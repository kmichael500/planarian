using System.Net;
using System.Text.Json;
using Planarian.Library.Exceptions;
using Planarian.Model.Shared;
using Planarian.Modules.Map.Models;
using Planarian.Modules.Map.Services;
using Planarian.Modules.Map.Services.Hydrology;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Map.Controllers;

public class MapService : ServiceBase<MapRepository>
{
    private readonly GeologicMapHttpClient _geologicMapHttpClient;
    private readonly IReadOnlyCollection<IHydrologyProvider> _hydrologyProviders;
    private readonly UsgsWaterDataClient _usgsWaterDataClient;

    public MapService(
        MapRepository repository,
        RequestUser requestUser,
        GeologicMapHttpClient geologicMapHttpClient,
        IEnumerable<IHydrologyProvider> hydrologyProviders,
        UsgsWaterDataClient usgsWaterDataClient) : base(repository, requestUser)
    {
        _geologicMapHttpClient = geologicMapHttpClient;
        _hydrologyProviders = hydrologyProviders.ToList();
        _usgsWaterDataClient = usgsWaterDataClient;
    }

    public Task<IEnumerable<object>> GetMapData(
        double north, double south, double east, double west, int zoom, CancellationToken cancellationToken) =>
        Repository.GetMapData(north, south, east, west, zoom, cancellationToken);

    public async Task<CoordinateDto> GetMapCenter()
    {
        var result = await Repository.GetMapCenter();

        return result;
    }

    public async Task<byte[]?> GetEntrancesMVTAsync(
        int z,
        int x,
        int y,
        FilterQuery filterQuery,
        CancellationToken cancellationToken)
    {
        filterQuery ??= new FilterQuery();

        var result = await Repository.GetEntrancesMVTAsync(z, x, y, filterQuery, cancellationToken);
        return result;
    }
    
    public Task<List<string>> GetLinePlotIds(
        double north, double south, double east, double west, double zoom, CancellationToken ct) =>
        Repository.GetLinePlotIds(north, south, east, west, zoom, ct);

    public Task<System.Text.Json.JsonElement?> GetLinePlotGeoJson(
        string plotId, CancellationToken ct) =>
        Repository.GetLinePlotGeoJson(plotId, ct);

    public Task<IReadOnlyList<NearbyStreamGage>> GetNearbyStreamGages(
        StreamGageRequest request,
        CancellationToken cancellationToken)
    {
        HydrologyRequestValidator.ValidateNearbyGages(request);
        return ExecuteHydrologyRequest(
            () => _usgsWaterDataClient.GetNearbyStreamGagesAsync(
                request.Origins,
                request.DistanceMiles,
                cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<StreamGageParameter>> GetStreamGageObservations(
        string siteCode,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        HydrologyRequestValidator.ValidateObservations(siteCode, startDate, endDate);
        return ExecuteHydrologyRequest(
            () => _usgsWaterDataClient.GetStreamGageObservationsAsync(
                siteCode,
                startDate,
                endDate,
                cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<StreamGageLocation>> GetStreamGagesInBounds(
        double north,
        double south,
        double east,
        double west,
        CancellationToken cancellationToken)
    {
        HydrologyRequestValidator.ValidateStreamGageBounds(north, south, east, west);
        return ExecuteHydrologyRequest(
            () => _usgsWaterDataClient.GetStreamGagesInBoundsAsync(
                north, south, east, west, cancellationToken),
            cancellationToken);
    }

    public Task<StreamGagePeakSummary> GetStreamGagePeakSummary(
        string siteCode,
        CancellationToken cancellationToken)
    {
        HydrologyRequestValidator.ValidateSiteCode(siteCode);
        return ExecuteHydrologyRequest(
            () => _usgsWaterDataClient.GetStreamGagePeakSummaryAsync(siteCode, cancellationToken),
            cancellationToken);
    }

    public async Task<IReadOnlyList<HydrologyFeature>> GetHydrologyFeaturesInBounds(
        double north,
        double south,
        double east,
        double west,
        CancellationToken cancellationToken)
    {
        HydrologyRequestValidator.ValidateHydrologyBounds(north, south, east, west);
        return await ExecuteHydrologyRequest(async () =>
        {
            var providerResults = await Task.WhenAll(
                _hydrologyProviders.Select(provider =>
                    provider.GetFeaturesInBoundsAsync(north, south, east, west, cancellationToken)));

            return providerResults.SelectMany(features => features).ToList();
        }, cancellationToken);
    }

    private static async Task<T> ExecuteHydrologyRequest<T>(
        Func<Task<T>> request,
        CancellationToken requestCancellation)
    {
        try
        {
            return await request();
        }
        catch (OperationCanceledException exception) when (!requestCancellation.IsCancellationRequested)
        {
            throw new ApiException(
                StatusCodes.Status504GatewayTimeout,
                ApiExceptionType.InternalServerError,
                "The hydrology data provider timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            var statusCode = exception.StatusCode == HttpStatusCode.TooManyRequests
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status502BadGateway;
            throw new ApiException(
                statusCode,
                ApiExceptionType.InternalServerError,
                "The hydrology data provider is temporarily unavailable.",
                exception);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new ApiException(
                StatusCodes.Status502BadGateway,
                ApiExceptionType.InternalServerError,
                "The hydrology data provider returned an invalid response.",
                exception);
        }
    }

    public async Task<IEnumerable<GeologicMapResult>> GetGeologicMaps(double latitude, double longitude, CancellationToken cancellationToken)
    {
        var result = await _geologicMapHttpClient.GetMapsAsync(latitude, longitude, cancellationToken);

        return result.Results;
    }

    public Task<GeologicTileResult?> GetGeologicTile(
        string scale,
        int z,
        int x,
        int y,
        CancellationToken cancellationToken) =>
        _geologicMapHttpClient.GetTileAsync(scale, z, x, y, cancellationToken);

}
