using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Planarian.Modules.Map.Models;

namespace Planarian.Modules.Map.Services.Hydrology;

public sealed class Usgs3DhpHydrologyProvider : IHydrologyProvider
{
    private const int PageSize = 2500;
    private readonly HttpClient _httpClient;

    public Usgs3DhpHydrologyProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(
            "https://3dhp.nationalmap.gov/arcgis/rest/services/usgs_3dhp_all/FeatureServer/");
    }

    public Task<IReadOnlyList<HydrologyFeature>> GetFeaturesInBoundsAsync(
        double north,
        double south,
        double east,
        double west,
        CancellationToken cancellationToken) =>
        GetFeaturesAsync(
            offset => BuildBoundsRequestUri(north, south, east, west, offset),
            cancellationToken);

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
