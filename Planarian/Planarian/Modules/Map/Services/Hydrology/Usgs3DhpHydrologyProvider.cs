using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Planarian.Modules.Map.Models;

namespace Planarian.Modules.Map.Services.Hydrology;

public sealed class Usgs3DhpHydrologyProvider : IHydrologyProvider
{
    private const int PageSize = 2500;
    private const double BoundsCacheGridDegrees = 0.05;
    private static readonly TimeSpan BoundsCacheLifetime = TimeSpan.FromHours(6);
    private readonly HttpClient _httpClient;
    private readonly HydrologyMemoryCache _cache;

    public Usgs3DhpHydrologyProvider(HttpClient httpClient, HydrologyMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
        _httpClient.BaseAddress = new Uri(
            "https://3dhp.nationalmap.gov/arcgis/rest/services/usgs_3dhp_all/FeatureServer/");
    }

    public async Task<IReadOnlyList<HydrologyFeature>> GetFeaturesInBoundsAsync(
        double north,
        double south,
        double east,
        double west,
        CancellationToken cancellationToken)
    {
        var expanded = ExpandBoundsForCache(north, south, east, west);
        var key = FormattableString.Invariant(
            $"usgs:3dhp-bounds:{expanded.South:F4}:{expanded.North:F4}:{expanded.West:F4}:{expanded.East:F4}");
        var cached = await _cache.GetOrCreateAsync(
            key,
            BoundsCacheLifetime,
            sharedCancellation => GetFeaturesAsync(
                offset => BuildBoundsRequestUri(expanded.North, expanded.South, expanded.East, expanded.West, offset),
                sharedCancellation),
            features => Math.Max(1, features.Count),
            cancellationToken);

        return cached
            .Where(feature => feature.Latitude >= south && feature.Latitude <= north &&
                              feature.Longitude >= west && feature.Longitude <= east)
            .ToList();
    }

    private async Task<IReadOnlyList<HydrologyFeature>> GetFeaturesAsync(
        Func<int, string> buildRequestUri,
        CancellationToken cancellationToken)
    {
        var results = new List<HydrologyFeature>();
        var offset = 0;
        bool hasMore;
        do
        {
            using var response = await _httpClient.GetAsync(buildRequestUri(offset), cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<Usgs3DhpResponse>(
                stream,
                cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("USGS 3DHP returned an empty response.");

            if (payload.Error is not null)
            {
                throw new InvalidOperationException(
                    $"USGS 3DHP query failed: {payload.Error.Message ?? "Unknown error"}");
            }

            foreach (var feature in payload.Features)
            {
                var normalized = NormalizeFeature(feature);
                if (normalized is not null) results.Add(normalized);
            }

            offset += payload.Features.Count;
            hasMore = payload.ExceededTransferLimit && payload.Features.Count > 0;
        } while (hasMore);

        return results;
    }

    private static string BuildBoundsRequestUri(
        double north,
        double south,
        double east,
        double west,
        int offset)
    {
        var geometry = FormattableString.Invariant($"{west},{south},{east},{north}");
        var query = new Dictionary<string, string>
        {
            ["where"] = "featuretype IN (3,7,8)",
            ["geometry"] = geometry,
            ["geometryType"] = "esriGeometryEnvelope",
            ["inSR"] = "4326",
            ["outSR"] = "4326",
            ["spatialRel"] = "esriSpatialRelIntersects",
            ["outFields"] = "id3dhp,gnisidlabel,featuretype,featuretypelabel",
            ["returnGeometry"] = "true",
            ["returnZ"] = "false",
            ["orderByFields"] = "OBJECTID",
            ["resultOffset"] = offset.ToString(CultureInfo.InvariantCulture),
            ["resultRecordCount"] = PageSize.ToString(CultureInfo.InvariantCulture),
            ["f"] = "json"
        };

        return "20/query?" + string.Join(
            "&",
            query.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static SearchBounds ExpandBoundsForCache(double north, double south, double east, double west)
    {
        static double FloorGrid(double value) =>
            Math.Floor(value / BoundsCacheGridDegrees) * BoundsCacheGridDegrees;
        static double CeilingGrid(double value) =>
            Math.Ceiling(value / BoundsCacheGridDegrees) * BoundsCacheGridDegrees;

        return new SearchBounds(
            Math.Max(-90d, FloorGrid(south)),
            Math.Min(90d, CeilingGrid(north)),
            Math.Max(-180d, FloorGrid(west)),
            Math.Min(180d, CeilingGrid(east)));
    }

    private static HydrologyFeature? NormalizeFeature(Usgs3DhpFeature feature)
    {
        if (feature.Geometry is null || feature.Attributes is null) return null;

        var featureType = feature.Attributes.FeatureType switch
        {
            3 => "Waterbody Outlet",
            7 => "Spring",
            8 => "Sink",
            _ => feature.Attributes.FeatureTypeLabel
        };
        if (string.IsNullOrWhiteSpace(featureType)) return null;

        var id = string.IsNullOrWhiteSpace(feature.Attributes.Id3Dhp)
            ? FormattableString.Invariant($"{featureType}:{feature.Geometry.Y:F6},{feature.Geometry.X:F6}")
            : feature.Attributes.Id3Dhp;

        return new HydrologyFeature(
            id,
            string.IsNullOrWhiteSpace(feature.Attributes.GnisLabel) ? null : feature.Attributes.GnisLabel,
            featureType,
            feature.Geometry.Y,
            feature.Geometry.X,
            "USGS 3DHP");
    }

    private sealed record SearchBounds(double South, double North, double West, double East);

    private sealed class Usgs3DhpResponse
    {
        [JsonPropertyName("features")]
        public List<Usgs3DhpFeature> Features { get; init; } = [];

        [JsonPropertyName("exceededTransferLimit")]
        public bool ExceededTransferLimit { get; init; }

        [JsonPropertyName("error")]
        public ArcGisError? Error { get; init; }
    }

    private sealed class Usgs3DhpFeature
    {
        [JsonPropertyName("attributes")]
        public Usgs3DhpAttributes? Attributes { get; init; }

        [JsonPropertyName("geometry")]
        public Usgs3DhpGeometry? Geometry { get; init; }
    }

    private sealed class Usgs3DhpAttributes
    {
        [JsonPropertyName("id3dhp")]
        public string? Id3Dhp { get; init; }

        [JsonPropertyName("gnisidlabel")]
        public string? GnisLabel { get; init; }

        [JsonPropertyName("featuretype")]
        public int FeatureType { get; init; }

        [JsonPropertyName("featuretypelabel")]
        public string? FeatureTypeLabel { get; init; }
    }

    private sealed class Usgs3DhpGeometry
    {
        [JsonPropertyName("x")]
        public double X { get; init; }

        [JsonPropertyName("y")]
        public double Y { get; init; }
    }

    private sealed class ArcGisError
    {
        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
