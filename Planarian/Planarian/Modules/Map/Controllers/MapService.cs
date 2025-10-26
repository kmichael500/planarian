using Microsoft.AspNetCore.Http.HttpResults;
using Planarian.Model.Shared;
using Planarian.Modules.Map.Services;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Map.Controllers;

public class MapService : ServiceBase<MapRepository>
{
    private readonly GeologicMapHttpClient _geologicMapHttpClient;
    public MapService(MapRepository repository, RequestUser requestUser, GeologicMapHttpClient geologicMapHttpClient) : base(repository, requestUser)
    {
        _geologicMapHttpClient = geologicMapHttpClient;
    }

    public async Task<IEnumerable<object>> GetMapData(double north, double south, double east, double west, int zoom,
        CancellationToken cancellationToken)
    {
        var result = await Repository.GetMapData(north, south, east, west, zoom, cancellationToken);

        return result;
    }

    public async Task<CoordinateDto> GetMapCenter()
    {
        var result = await Repository.GetMapCenter();

        return result;
    }

    public async Task<byte[]?> GetEntrancesMVTAsync(int z, int x, int y, FilterQuery filterQuery, CancellationToken cancellationToken)
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

    public async Task<IEnumerable<GeologicMapResult>> GetGeologicMaps(double latitude, double longitude, CancellationToken cancellationToken)
    {
        var result = await _geologicMapHttpClient.GetMapsAsync(latitude, longitude, cancellationToken);

        return result.Results;
    }

}
