using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Planarian.Modules.Map.Models;

namespace Planarian.Modules.Map.Services.Hydrology;

public sealed class UsgsWaterDataClient
{
    private const double BoundsCacheGridDegrees = 0.05;
    private static readonly TimeSpan NearbyGageCacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ObservationCacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BoundsCacheLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan PeakCacheLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ObservationBucket = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly HydrologyMemoryCache _cache;
    private readonly ILogger<UsgsWaterDataClient> _logger;

    public UsgsWaterDataClient(
        HttpClient httpClient,
        UsgsWaterDataOptions options,
        HydrologyMemoryCache cache,
        ILogger<UsgsWaterDataClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://api.waterdata.usgs.gov/ogcapi/v1/collections/");

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
    }

    public Task<IReadOnlyList<NearbyStreamGage>> GetNearbyStreamGagesAsync(
        IReadOnlyList<StreamGageSearchOrigin> origins,
        double distanceMiles,
        CancellationToken cancellationToken)
    {
        if (origins.Count == 0) return Task.FromResult<IReadOnlyList<NearbyStreamGage>>([]);

        return _cache.GetOrCreateAsync(
            BuildNearbyCacheKey(origins, distanceMiles),
            NearbyGageCacheLifetime,
            sharedCancellation => GetNearbyStreamGagesUncachedAsync(origins, distanceMiles, sharedCancellation),
            GetNearbyCacheSize,
            cancellationToken);
    }

    private async Task<IReadOnlyList<NearbyStreamGage>> GetNearbyStreamGagesUncachedAsync(
        IReadOnlyList<StreamGageSearchOrigin> origins,
        double distanceMiles,
        CancellationToken cancellationToken)
    {
        var summaryEnd = DateTimeOffset.UtcNow;
        var summaryStart = summaryEnd.AddHours(-24);
        var series = await GetUsgsSeriesMetadataAsync(origins, distanceMiles, summaryStart, summaryEnd, cancellationToken);
        if (series.Count == 0) return [];

        var pointsBySeries = await GetObservationPointsAsync(
            series.Select(item => item.SeriesId).ToList(), summaryStart, summaryEnd, cancellationToken);

        return series
            .GroupBy(item => item.MonitoringLocationId)
            .Select(group => UsgsWaterDataProjector.BuildGage(group.ToList(), pointsBySeries, summaryOnly: true))
            .Where(gage => gage.Parameters.Any(parameter => parameter.Points.Count > 0))
            .OrderBy(gage => gage.DistanceMiles)
            .ToList();
    }

    public async Task<IReadOnlyList<StreamGageParameter>> GetStreamGageObservationsAsync(
        string siteCode,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        var bucketStart = FloorToBucket(startDate, ObservationBucket);
        var bucketEnd = CeilingToBucket(endDate, ObservationBucket);
        var key = $"usgs:observations:{siteCode}:{bucketStart.UtcDateTime.Ticks}:{bucketEnd.UtcDateTime.Ticks}";
        var cached = await _cache.GetOrCreateAsync(
            key,
            ObservationCacheLifetime,
            sharedCancellation => GetStreamGageObservationsUncachedAsync(
                siteCode, bucketStart, bucketEnd, sharedCancellation),
            GetObservationCacheSize,
            cancellationToken);

        return cached
            .Select(parameter => parameter with
            {
                Points = parameter.Points
                    .Where(point => point.DateTime >= startDate && point.DateTime <= endDate)
                    .ToList()
            })
            .ToList();
    }

    private async Task<IReadOnlyList<StreamGageParameter>> GetStreamGageObservationsUncachedAsync(
        string siteCode,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        var series = await GetUsgsSeriesMetadataForSiteAsync(siteCode, startDate, endDate, cancellationToken);
        if (series.Count == 0) return [];

        var pointsBySeries = await GetObservationPointsAsync(
            series.Select(item => item.SeriesId).ToList(), startDate, endDate, cancellationToken);

        return UsgsWaterDataProjector.BuildParameters(series, pointsBySeries);
    }

    public async Task<IReadOnlyList<StreamGageLocation>> GetStreamGagesInBoundsAsync(
        double north,
        double south,
        double east,
        double west,
        CancellationToken cancellationToken)
    {
        var expanded = ExpandBoundsForCache(north, south, east, west);
        var key = FormattableString.Invariant(
            $"usgs:gage-bounds:{expanded.South:F4}:{expanded.North:F4}:{expanded.West:F4}:{expanded.East:F4}");
        var cached = await _cache.GetOrCreateAsync(
            key,
            BoundsCacheLifetime,
            sharedCancellation => GetStreamGagesInBoundsUncachedAsync(expanded, sharedCancellation),
            locations => Math.Max(1, locations.Count),
            cancellationToken);

        return cached
            .Where(location => location.Latitude >= south && location.Latitude <= north &&
                               location.Longitude >= west && location.Longitude <= east)
            .ToList();
    }

    private async Task<IReadOnlyList<StreamGageLocation>> GetStreamGagesInBoundsUncachedAsync(
        UsgsSearchBounds bounds,
        CancellationToken cancellationToken)
    {
        var locations = new Dictionary<string, StreamGageLocation>();
        var activeSince = DateTimeOffset.UtcNow.AddDays(-30);
        var requestUri = UsgsWaterDataRequestBuilder.BuildMetadataRequestUri(bounds);

        await ForEachFeatureAsync(requestUri, feature =>
        {
            var location = UsgsWaterDataFeatureParser.ParseStreamGageLocation(feature, activeSince);
            if (location is not null) locations[location.Id] = location;
        }, cancellationToken);

        return locations.Values.OrderBy(location => location.SiteName).ToList();
    }

    public Task<StreamGagePeakSummary> GetStreamGagePeakSummaryAsync(
        string siteCode,
        CancellationToken cancellationToken) =>
        _cache.GetOrCreateAsync(
            $"usgs:peaks:{siteCode}",
            PeakCacheLifetime,
            sharedCancellation => GetStreamGagePeakSummaryUncachedAsync(siteCode, sharedCancellation),
            summary => Math.Max(1, summary.StreamflowHistory.Count + summary.GageHeightHistory.Count),
            cancellationToken);

    private async Task<StreamGagePeakSummary> GetStreamGagePeakSummaryUncachedAsync(
        string siteCode,
        CancellationToken cancellationToken)
    {
        var peaks = new List<UsgsPeakObservation>();
        await ForEachFeatureAsync(UsgsWaterDataRequestBuilder.BuildPeakRequestUri(siteCode), feature =>
        {
            var peak = UsgsWaterDataFeatureParser.ParsePeakObservation(feature);
            if (peak is not null) peaks.Add(peak);
        }, cancellationToken);

        return UsgsWaterDataProjector.BuildPeakSummary(siteCode, peaks);
    }

    private async Task<IReadOnlyList<UsgsSeriesMetadata>> GetUsgsSeriesMetadataAsync(
        IReadOnlyList<StreamGageSearchOrigin> origins,
        double distanceMiles,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        var bounds = GetUsgsSearchBounds(origins, distanceMiles);
        var requestUri = UsgsWaterDataRequestBuilder.BuildMetadataRequestUri(bounds);
        var candidates = new List<UsgsSeriesMetadata>();

        await ForEachFeatureAsync(requestUri, feature =>
        {
            var parsed = UsgsWaterDataFeatureParser.ParseSeriesMetadata(
                feature,
                origins,
                distanceMiles,
                startDate,
                endDate);
            if (parsed is not null) candidates.Add(parsed);
        }, cancellationToken);

        return candidates
            .GroupBy(item => (item.MonitoringLocationId, item.ParameterCode))
            .Select(group => group
                .OrderByDescending(item => string.Equals(item.Primary, "Primary", StringComparison.OrdinalIgnoreCase))
                .First())
            .ToList();
    }

    private async Task<IReadOnlyList<UsgsSiteSeriesMetadata>> GetUsgsSeriesMetadataForSiteAsync(
        string siteCode,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        var candidates = new List<UsgsSiteSeriesMetadata>();
        await ForEachFeatureAsync(UsgsWaterDataRequestBuilder.BuildSiteMetadataRequestUri(siteCode), feature =>
        {
            var parsed = UsgsWaterDataFeatureParser.ParseSiteSeriesMetadata(feature, startDate, endDate);
            if (parsed is not null) candidates.Add(parsed);
        }, cancellationToken);

        return candidates
            .GroupBy(item => item.ParameterCode)
            .Select(group => group
                .OrderByDescending(item => string.Equals(item.Primary, "Primary", StringComparison.OrdinalIgnoreCase))
                .First())
            .ToList();
    }

    private async Task<Dictionary<string, List<StreamGagePoint>>> GetObservationPointsAsync(
        IReadOnlyList<string> seriesIds,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        var result = seriesIds.ToDictionary(id => id, _ => new List<StreamGagePoint>());

        foreach (var seriesBatch in seriesIds.Chunk(100))
        {
            var requestUri = UsgsWaterDataRequestBuilder.BuildObservationRequestUri(seriesBatch, startDate, endDate);
            await ForEachFeatureAsync(requestUri, feature =>
            {
                var parsed = UsgsWaterDataFeatureParser.ParseObservationPoint(feature);
                if (parsed is not null && result.TryGetValue(parsed.SeriesId, out var points))
                    points.Add(parsed.Point);
            }, cancellationToken);
        }

        foreach (var points in result.Values)
            points.Sort((a, b) => a.DateTime.CompareTo(b.DateTime));
        return result;
    }

    private async Task ForEachFeatureAsync(
        string requestUri,
        Action<JsonElement> onFeature,
        CancellationToken cancellationToken)
    {
        string? next = requestUri;
        while (next is not null)
        {
            using var response = await _httpClient.GetAsync(next, cancellationToken);
            LogRateLimit(response);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.TryGetProperty("features", out var features))
            {
                foreach (var feature in features.EnumerateArray()) onFeature(feature);
            }

            next = null;
            if (!root.TryGetProperty("links", out var links)) continue;
            foreach (var link in links.EnumerateArray())
            {
                if (UsgsWaterDataFeatureParser.GetString(link, "rel") != "next") continue;
                next = UsgsWaterDataFeatureParser.GetString(link, "href");
                break;
            }
        }
    }

    private void LogRateLimit(HttpResponseMessage response)
    {
        var limit = GetRateLimitHeader(response, "X-RateLimit-Limit");
        var remaining = GetRateLimitHeader(response, "X-RateLimit-Remaining");
        var path = response.RequestMessage?.RequestUri?.AbsolutePath ?? "USGS Water Data API";

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning(
                "USGS Water Data API rate limit exceeded for {Path}. Retry-After: {RetryAfter}",
                path,
                response.Headers.RetryAfter?.ToString() ?? "not provided");
            return;
        }

        if (limit is null || remaining is null) return;

        var warningThreshold = Math.Max(10, (int)Math.Ceiling(limit.Value * 0.1));
        if (remaining <= warningThreshold)
        {
            _logger.LogWarning(
                "USGS Water Data API rate limit is low for {Path}: {Remaining}/{Limit} requests remain",
                path, remaining, limit);
        }
        else
        {
            _logger.LogDebug(
                "USGS Water Data API rate limit for {Path}: {Remaining}/{Limit} requests remain",
                path, remaining, limit);
        }
    }

    private static int? GetRateLimitHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) &&
        int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static string BuildNearbyCacheKey(
        IReadOnlyList<StreamGageSearchOrigin> origins,
        double distanceMiles)
    {
        var originKey = string.Join(";", origins
            .OrderBy(origin => origin.Id, StringComparer.Ordinal)
            .ThenBy(origin => origin.Name, StringComparer.Ordinal)
            .Select(origin => string.Join(":",
                Uri.EscapeDataString(origin.Id),
                Uri.EscapeDataString(origin.Name),
                origin.Latitude.ToString("R", CultureInfo.InvariantCulture),
                origin.Longitude.ToString("R", CultureInfo.InvariantCulture))));
        var originHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(originKey)));
        return $"usgs:nearby:{distanceMiles.ToString("R", CultureInfo.InvariantCulture)}:{originHash}";
    }

    private static long GetNearbyCacheSize(IReadOnlyList<NearbyStreamGage> gages) =>
        Math.Max(1L, gages.Sum(gage =>
            1L + gage.Parameters.Sum(parameter => (long)parameter.Points.Count)));

    private static long GetObservationCacheSize(IReadOnlyList<StreamGageParameter> parameters) =>
        Math.Max(1L, parameters.Sum(parameter => (long)parameter.Points.Count));

    private static DateTimeOffset FloorToBucket(DateTimeOffset value, TimeSpan bucket)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % bucket.Ticks, TimeSpan.Zero);
    }

    private static DateTimeOffset CeilingToBucket(DateTimeOffset value, TimeSpan bucket)
    {
        var utc = value.ToUniversalTime();
        var floor = FloorToBucket(utc, bucket);
        return floor == utc ? floor : floor.Add(bucket);
    }

    private static UsgsSearchBounds ExpandBoundsForCache(double north, double south, double east, double west)
    {
        static double FloorGrid(double value) => Math.Floor(value / BoundsCacheGridDegrees) * BoundsCacheGridDegrees;
        static double CeilingGrid(double value) => Math.Ceiling(value / BoundsCacheGridDegrees) * BoundsCacheGridDegrees;

        return new UsgsSearchBounds(
            Math.Max(-90d, FloorGrid(south)),
            Math.Min(90d, CeilingGrid(north)),
            Math.Max(-180d, FloorGrid(west)),
            Math.Min(180d, CeilingGrid(east)));
    }

    private static UsgsSearchBounds GetUsgsSearchBounds(
        IReadOnlyList<StreamGageSearchOrigin> origins,
        double distanceMiles)
    {
        var south = 90d;
        var north = -90d;
        var west = 180d;
        var east = -180d;

        foreach (var origin in origins)
        {
            var latitudeDelta = distanceMiles / 69d;
            var longitudeMilesPerDegree = 69d * Math.Max(Math.Abs(Math.Cos(origin.Latitude * Math.PI / 180d)), 0.01d);
            var longitudeDelta = distanceMiles / longitudeMilesPerDegree;
            south = Math.Min(south, origin.Latitude - latitudeDelta);
            north = Math.Max(north, origin.Latitude + latitudeDelta);
            west = Math.Min(west, origin.Longitude - longitudeDelta);
            east = Math.Max(east, origin.Longitude + longitudeDelta);
        }

        return new UsgsSearchBounds(
            Math.Max(-90d, south),
            Math.Min(90d, north),
            Math.Max(-180d, west),
            Math.Min(180d, east));
    }

}
