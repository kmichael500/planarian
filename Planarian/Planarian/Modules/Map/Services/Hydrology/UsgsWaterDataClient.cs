using System.Globalization;
using System.Net;
using System.Text.Json;
using Planarian.Modules.Map.Models;

namespace Planarian.Modules.Map.Services.Hydrology;

public sealed class UsgsWaterDataClient
{
    private const double EarthRadiusMiles = 3958.7613;
    private const int PageSize = 50000;
    private const double BoundsCacheGridDegrees = 0.05;
    private static readonly TimeSpan NearbyGageCacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ObservationCacheLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BoundsCacheLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan PeakCacheLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ObservationBucket = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly UsgsWaterDataCache _cache;
    private readonly ILogger<UsgsWaterDataClient> _logger;

    public UsgsWaterDataClient(
        HttpClient httpClient,
        UsgsWaterDataOptions options,
        UsgsWaterDataCache cache,
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
            () => GetNearbyStreamGagesUncachedAsync(origins, distanceMiles, CancellationToken.None),
            cancellationToken);
    }

    private async Task<IReadOnlyList<NearbyStreamGage>> GetNearbyStreamGagesUncachedAsync(
        IReadOnlyList<StreamGageSearchOrigin> origins,
        double distanceMiles,
        CancellationToken cancellationToken)
    {
        var summaryEnd = DateTimeOffset.UtcNow;
        var summaryStart = summaryEnd.AddHours(-24);
        var series = await GetSeriesMetadataAsync(origins, distanceMiles, summaryStart, summaryEnd, cancellationToken);
        if (series.Count == 0) return [];

        var pointsBySeries = await GetObservationPointsAsync(
            series.Select(item => item.SeriesId).ToList(), summaryStart, summaryEnd, cancellationToken);

        return series
            .GroupBy(item => item.MonitoringLocationId)
            .Select(group => BuildGage(group.ToList(), pointsBySeries, summaryOnly: true))
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
            () => GetStreamGageObservationsUncachedAsync(siteCode, bucketStart, bucketEnd, CancellationToken.None),
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
        var series = await GetSeriesMetadataForSiteAsync(siteCode, startDate, endDate, cancellationToken);
        if (series.Count == 0) return [];

        var pointsBySeries = await GetObservationPointsAsync(
            series.Select(item => item.SeriesId).ToList(), startDate, endDate, cancellationToken);

        return series
            .OrderBy(item => item.ParameterCode)
            .Select(item => new StreamGageParameter(
                item.ParameterCode,
                item.ParameterCode == "00060" ? "Streamflow" : "Gage height",
                item.Unit,
                pointsBySeries.TryGetValue(item.SeriesId, out var points) ? points : []))
            .ToList();
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
            () => GetStreamGagesInBoundsUncachedAsync(expanded, CancellationToken.None),
            cancellationToken);

        return cached
            .Where(location => location.Latitude >= south && location.Latitude <= north &&
                               location.Longitude >= west && location.Longitude <= east)
            .ToList();
    }

    private async Task<IReadOnlyList<StreamGageLocation>> GetStreamGagesInBoundsUncachedAsync(
        SearchBounds bounds,
        CancellationToken cancellationToken)
    {
        var locations = new Dictionary<string, StreamGageLocation>();
        var activeSince = DateTimeOffset.UtcNow.AddDays(-30);
        var requestUri = BuildMetadataRequestUri(bounds);

        await ForEachFeatureAsync(requestUri, feature =>
        {
            var location = ParseStreamGageLocation(feature, activeSince);
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
            () => GetStreamGagePeakSummaryUncachedAsync(siteCode, CancellationToken.None),
            cancellationToken);

    private async Task<StreamGagePeakSummary> GetStreamGagePeakSummaryUncachedAsync(
        string siteCode,
        CancellationToken cancellationToken)
    {
        var peaks = new List<PeakObservation>();
        await ForEachFeatureAsync(BuildPeakRequestUri(siteCode), feature =>
        {
            var peak = ParsePeakObservation(feature);
            if (peak is not null) peaks.Add(peak);
        }, cancellationToken);

        return new StreamGagePeakSummary(
            siteCode,
            BuildHistoricalPeak(peaks, "00060", "Streamflow"),
            BuildHistoricalPeak(peaks, "00065", "Gage height"),
            BuildAnnualPeakHistory(peaks, "00060", "Streamflow"),
            BuildAnnualPeakHistory(peaks, "00065", "Gage height"));
    }

    private async Task<IReadOnlyList<SeriesMetadata>> GetSeriesMetadataAsync(
        IReadOnlyList<StreamGageSearchOrigin> origins,
        double distanceMiles,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        var bounds = GetSearchBounds(origins, distanceMiles);
        var requestUri = BuildMetadataRequestUri(bounds);
        var candidates = new List<SeriesMetadata>();

        await ForEachFeatureAsync(requestUri, feature =>
        {
            var parsed = ParseSeriesMetadata(
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

    private async Task<IReadOnlyList<SiteSeriesMetadata>> GetSeriesMetadataForSiteAsync(
        string siteCode,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        var candidates = new List<SiteSeriesMetadata>();
        await ForEachFeatureAsync(BuildSiteMetadataRequestUri(siteCode), feature =>
        {
            var parsed = ParseSiteSeriesMetadata(feature, startDate, endDate);
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
            var requestUri = BuildObservationRequestUri(seriesBatch, startDate, endDate);
            await ForEachFeatureAsync(requestUri, feature =>
            {
                var properties = feature.GetProperty("properties");
                var seriesId = GetString(properties, "time_series_id");
                var value = GetString(properties, "value");
                var time = GetString(properties, "time");
                if (seriesId is null || value is null || time is null ||
                    !result.TryGetValue(seriesId, out var points))
                    return;
                if (!DateTimeOffset.TryParse(
                        time,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var dateTime))
                    return;
                points.Add(new StreamGagePoint(
                    value,
                    dateTime,
                    GetString(properties, "approval_status")));
            }, cancellationToken);
        }

        foreach (var points in result.Values)
            points.Sort((a, b) => a.DateTime.CompareTo(b.DateTime));
        return result;
    }

    private static NearbyStreamGage BuildGage(
        IReadOnlyList<SeriesMetadata> series,
        IReadOnlyDictionary<string, List<StreamGagePoint>> pointsBySeries,
        bool summaryOnly = false)
    {
        var first = series[0];
        var parameters = series
            .OrderBy(item => item.ParameterCode)
            .Select(item =>
            {
                IReadOnlyList<StreamGagePoint> points = pointsBySeries.TryGetValue(item.SeriesId, out var seriesPoints)
                    ? seriesPoints
                    : [];
                return new StreamGageParameter(
                    item.ParameterCode,
                    item.ParameterCode == "00060" ? "Streamflow" : "Gage height",
                    item.Unit,
                    summaryOnly ? GetSummaryPoints(points) : points);
            })
            .ToList();

        return new NearbyStreamGage(
            first.MonitoringLocationId,
            first.SiteCode,
            first.SiteName,
            first.Latitude,
            first.Longitude,
            first.DistanceMiles,
            first.NearestOrigin.Id,
            first.NearestOrigin.Name,
            first.DrainageAreaSquareMiles,
            first.ContributingDrainageAreaSquareMiles,
            parameters);
    }

    private static IReadOnlyList<StreamGagePoint> GetSummaryPoints(IReadOnlyList<StreamGagePoint> points)
    {
        if (points.Count <= 1) return points;

        var latest = points[^1];
        var comparisonTime = latest.DateTime.AddHours(-6);
        var comparison = points
            .Take(points.Count - 1)
            .MinBy(point => Math.Abs((point.DateTime - comparisonTime).TotalMinutes));
        if (comparison is null || Math.Abs((comparison.DateTime - comparisonTime).TotalMinutes) > 90)
            return [latest];

        return [comparison, latest];
    }

    private static StreamGageLocation? ParseStreamGageLocation(
        JsonElement feature,
        DateTimeOffset activeSince)
    {
        if (!feature.TryGetProperty("geometry", out var geometry) || geometry.ValueKind == JsonValueKind.Null) return null;
        if (!geometry.TryGetProperty("coordinates", out var coordinates) || coordinates.GetArrayLength() < 2) return null;
        if (!feature.TryGetProperty("properties", out var properties)) return null;

        var computation = GetString(properties, "computation_identifier");
        if (!string.Equals(computation, "Instantaneous", StringComparison.OrdinalIgnoreCase)) return null;
        if (DateTimeOffset.TryParse(
                GetString(properties, "end"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var seriesEnd) && seriesEnd < activeSince)
            return null;

        var locationId = GetString(properties, "monitoring_location_id");
        if (locationId is null) return null;

        return new StreamGageLocation(
            locationId,
            GetString(properties, "monitoring_location_number") ?? locationId.Replace("USGS-", ""),
            GetString(properties, "monitoring_location_name") ?? locationId,
            coordinates[1].GetDouble(),
            coordinates[0].GetDouble());
    }

    private static SeriesMetadata? ParseSeriesMetadata(
        JsonElement feature,
        IReadOnlyList<StreamGageSearchOrigin> origins,
        double distanceMiles,
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        if (!feature.TryGetProperty("geometry", out var geometry) || geometry.ValueKind == JsonValueKind.Null) return null;
        if (!geometry.TryGetProperty("coordinates", out var coordinates) || coordinates.GetArrayLength() < 2) return null;
        if (!feature.TryGetProperty("properties", out var properties)) return null;

        if (DateTimeOffset.TryParse(
                GetString(properties, "begin"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var seriesBegin) && seriesBegin > endDate)
            return null;
        if (DateTimeOffset.TryParse(
                GetString(properties, "end"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var seriesEnd) && seriesEnd < startDate)
            return null;

        var longitude = coordinates[0].GetDouble();
        var latitude = coordinates[1].GetDouble();
        var nearest = origins
            .Select(origin => new OriginDistance(origin, GetDistanceMiles(
                origin.Latitude, origin.Longitude, latitude, longitude)))
            .MinBy(candidate => candidate.DistanceMiles);
        if (nearest is null || nearest.DistanceMiles > distanceMiles) return null;

        var seriesId = GetString(feature, "id");
        var locationId = GetString(properties, "monitoring_location_id");
        var parameterCode = GetString(properties, "parameter_code");
        if (seriesId is null || locationId is null || parameterCode is null) return null;

        var computation = GetString(properties, "computation_identifier");
        if (!string.Equals(computation, "Instantaneous", StringComparison.OrdinalIgnoreCase)) return null;

        return new SeriesMetadata(
            seriesId,
            locationId,
            GetString(properties, "monitoring_location_number") ?? locationId.Replace("USGS-", ""),
            GetString(properties, "monitoring_location_name") ?? locationId,
            latitude,
            longitude,
            nearest.DistanceMiles,
            nearest.Origin,
            parameterCode,
            GetString(properties, "unit_of_measure") ?? "",
            GetString(properties, "primary"),
            GetNullableDouble(properties, "drainage_area"),
            GetNullableDouble(properties, "contributing_drainage_area"));
    }

    private static SiteSeriesMetadata? ParseSiteSeriesMetadata(
        JsonElement feature,
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        if (!feature.TryGetProperty("properties", out var properties)) return null;

        if (DateTimeOffset.TryParse(
                GetString(properties, "begin"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var seriesBegin) && seriesBegin > endDate)
            return null;
        if (DateTimeOffset.TryParse(
                GetString(properties, "end"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var seriesEnd) && seriesEnd < startDate)
            return null;

        var seriesId = GetString(feature, "id");
        var parameterCode = GetString(properties, "parameter_code");
        if (seriesId is null || parameterCode is null) return null;

        var computation = GetString(properties, "computation_identifier");
        if (!string.Equals(computation, "Instantaneous", StringComparison.OrdinalIgnoreCase)) return null;

        return new SiteSeriesMetadata(
            seriesId,
            parameterCode,
            GetString(properties, "unit_of_measure") ?? "",
            GetString(properties, "primary"));
    }

    private static PeakObservation? ParsePeakObservation(JsonElement feature)
    {
        if (!feature.TryGetProperty("properties", out var properties)) return null;
        var parameterCode = GetString(properties, "parameter_code");
        var value = GetString(properties, "value");
        if (parameterCode is not ("00060" or "00065") || value is null ||
            !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericValue))
            return null;

        var waterYearText = GetString(properties, "water_year");
        if (!int.TryParse(waterYearText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var waterYear))
            return null;

        DateOnly? date = null;
        var dateText = GetString(properties, "time");
        if (dateText is not null && DateOnly.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            date = parsedDate;

        int? peakSince = null;
        var peakSinceText = GetString(properties, "peak_since");
        if (int.TryParse(peakSinceText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPeakSince))
            peakSince = parsedPeakSince;

        return new PeakObservation(
            parameterCode,
            value,
            numericValue,
            GetString(properties, "unit_of_measure") ?? "",
            date,
            waterYear,
            GetStringValues(properties, "qualifier"),
            peakSince);
    }

    private static IReadOnlyList<StreamGageAnnualPeak> BuildAnnualPeakHistory(
        IReadOnlyList<PeakObservation> peaks,
        string parameterCode,
        string variableName) =>
        peaks
            .Where(peak => peak.ParameterCode == parameterCode && peak.PeakSince is null)
            .GroupBy(peak => peak.WaterYear)
            .Select(group => group.MaxBy(peak => peak.NumericValue)!)
            .OrderBy(peak => peak.WaterYear)
            .Select(peak => new StreamGageAnnualPeak(
                peak.ParameterCode,
                variableName,
                peak.Unit,
                peak.Value,
                peak.Date,
                peak.WaterYear,
                peak.Qualifiers))
            .ToList();

    private static StreamGageHistoricalPeak? BuildHistoricalPeak(
        IReadOnlyList<PeakObservation> peaks,
        string parameterCode,
        string variableName)
    {
        var matching = peaks
            .Where(peak => peak.ParameterCode == parameterCode && peak.PeakSince is null)
            .ToList();
        if (matching.Count == 0) return null;
        var highest = matching.MaxBy(peak => peak.NumericValue)!;
        return new StreamGageHistoricalPeak(
            parameterCode,
            variableName,
            highest.Unit,
            highest.Value,
            highest.Date,
            highest.WaterYear,
            highest.Qualifiers,
            matching.Min(peak => peak.WaterYear),
            matching.Max(peak => peak.WaterYear),
            matching.Select(peak => peak.WaterYear).Distinct().Count());
    }

    private static string BuildPeakRequestUri(string siteCode)
    {
        var query = new Dictionary<string, string>
        {
            ["monitoring_location_id"] = $"USGS-{siteCode}",
            ["parameter_code"] = "00060,00065",
            ["properties"] = "parameter_code,value,water_year,time,peak_since,unit_of_measure,qualifier",
            ["limit"] = PageSize.ToString(CultureInfo.InvariantCulture),
            ["f"] = "json"
        };
        return "peaks/items?" + ToQueryString(query);
    }

    private static string BuildMetadataRequestUri(SearchBounds bounds)
    {
        var query = new Dictionary<string, string>
        {
            ["bbox"] = FormattableString.Invariant($"{bounds.West},{bounds.South},{bounds.East},{bounds.North}"),
            ["agency_code"] = "USGS",
            ["site_type_code"] = "ST",
            ["parameter_code"] = "00060,00065",
            ["data_type"] = "Continuous values",
            ["properties"] = "monitoring_location_id,monitoring_location_number,monitoring_location_name,parameter_code,computation_identifier,unit_of_measure,primary,drainage_area,contributing_drainage_area,begin,end",
            ["limit"] = PageSize.ToString(CultureInfo.InvariantCulture),
            ["f"] = "json"
        };

        return "combined-metadata/items?" + ToQueryString(query);
    }

    private static string BuildSiteMetadataRequestUri(string siteCode)
    {
        var query = new Dictionary<string, string>
        {
            ["monitoring_location_id"] = $"USGS-{siteCode}",
            ["agency_code"] = "USGS",
            ["site_type_code"] = "ST",
            ["parameter_code"] = "00060,00065",
            ["data_type"] = "Continuous values",
            ["properties"] = "monitoring_location_id,monitoring_location_number,monitoring_location_name,parameter_code,computation_identifier,unit_of_measure,primary,drainage_area,contributing_drainage_area,begin,end",
            ["limit"] = PageSize.ToString(CultureInfo.InvariantCulture),
            ["f"] = "json"
        };

        return "combined-metadata/items?" + ToQueryString(query);
    }

    private static string BuildObservationRequestUri(
        IReadOnlyList<string> seriesIds,
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        var query = new Dictionary<string, string>
        {
            ["time_series_id"] = string.Join(",", seriesIds),
            ["datetime"] = $"{startDate.UtcDateTime:O}/{endDate.UtcDateTime:O}",
            ["properties"] = "time_series_id,value,time,approval_status",
            ["limit"] = PageSize.ToString(CultureInfo.InvariantCulture),
            ["f"] = "json"
        };
        return "continuous/items?" + ToQueryString(query);
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
                if (GetString(link, "rel") != "next") continue;
                next = GetString(link, "href");
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
        return $"usgs:nearby:{distanceMiles.ToString("R", CultureInfo.InvariantCulture)}:{originKey}";
    }

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

    private static SearchBounds ExpandBoundsForCache(double north, double south, double east, double west)
    {
        static double FloorGrid(double value) => Math.Floor(value / BoundsCacheGridDegrees) * BoundsCacheGridDegrees;
        static double CeilingGrid(double value) => Math.Ceiling(value / BoundsCacheGridDegrees) * BoundsCacheGridDegrees;

        return new SearchBounds(
            Math.Max(-90d, FloorGrid(south)),
            Math.Min(90d, CeilingGrid(north)),
            Math.Max(-180d, FloorGrid(west)),
            Math.Min(180d, CeilingGrid(east)));
    }

    private static SearchBounds GetSearchBounds(
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

        return new SearchBounds(
            Math.Max(-90d, south),
            Math.Min(90d, north),
            Math.Max(-180d, west),
            Math.Min(180d, east));
    }

    private static string ToQueryString(IReadOnlyDictionary<string, string> query) =>
        string.Join("&", query.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
            return null;
        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static IReadOnlyList<string> GetStringValues(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
            return [];
        if (property.ValueKind == JsonValueKind.Array)
            return property.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToList();
        var value = property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
        return string.IsNullOrWhiteSpace(value) ? [] : [value];
    }

    private static double? GetNullableDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
            return null;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var number)) return number;
        return double.TryParse(property.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static double GetDistanceMiles(double lat1, double lon1, double lat2, double lon2)
    {
        static double ToRadians(double degrees) => degrees * Math.PI / 180d;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2d), 2d) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Pow(Math.Sin(dLon / 2d), 2d);
        return EarthRadiusMiles * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
    }

    private sealed record SearchBounds(double South, double North, double West, double East);
    private sealed record OriginDistance(StreamGageSearchOrigin Origin, double DistanceMiles);
    private sealed record PeakObservation(
        string ParameterCode,
        string Value,
        double NumericValue,
        string Unit,
        DateOnly? Date,
        int WaterYear,
        IReadOnlyList<string> Qualifiers,
        int? PeakSince);

    private sealed record SiteSeriesMetadata(
        string SeriesId,
        string ParameterCode,
        string Unit,
        string? Primary);

    private sealed record SeriesMetadata(
        string SeriesId,
        string MonitoringLocationId,
        string SiteCode,
        string SiteName,
        double Latitude,
        double Longitude,
        double DistanceMiles,
        StreamGageSearchOrigin NearestOrigin,
        string ParameterCode,
        string Unit,
        string? Primary,
        double? DrainageAreaSquareMiles,
        double? ContributingDrainageAreaSquareMiles);
}
