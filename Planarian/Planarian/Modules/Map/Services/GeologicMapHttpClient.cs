using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Planarian.Modules.Map.Services;

public class GeologicMapHttpClient
{
    private const int PageSize = 50;
    private const int MinimumTileZoom = 4;
    private static readonly IReadOnlyDictionary<string, (string Service, int MaximumZoom)> TileServices =
        new Dictionary<string, (string Service, int MaximumZoom)>(StringComparer.OrdinalIgnoreCase)
        {
            ["500K"] = ("mvCache500K", 12),
            ["250K"] = ("mvCache250K", 12),
            ["125K"] = ("mvCache125K", 14),
            ["100K"] = ("mvCache100K", 14),
            ["63K"] = ("mvCache63K", 14),
            ["48K"] = ("mvCache48K", 14),
            ["24K"] = ("mvCache24K", 15)
        };
    private readonly HttpClient _httpClient;

    public GeologicMapHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://ngmdb.usgs.gov/");
    }

    public async Task<GeologicMapResponse> GetMapsAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        // prepare the LL–BB parameter
        var llbValue = $"[{longitude},{latitude}],[{longitude},{latitude}]";
        var encodedLlb = UrlEncoder.Default.Encode(llbValue);

        var allResults = new List<GeologicMapResult>();
        string? nextUrl = BuildPageUrl(encodedLlb, 1);

        while (!string.IsNullOrEmpty(nextUrl))
        {
            using var resp = await _httpClient.GetAsync(nextUrl, cancellationToken);
            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
            var page = await JsonSerializer.DeserializeAsync<GeologicMapResponse>(stream, cancellationToken: cancellationToken)
                       ?? throw new InvalidOperationException("Failed to deserialize geologic map response");

            allResults.AddRange(page.Results);
            nextUrl = page.Next;
        }

        return new GeologicMapResponse
        {
            Count    = allResults.Count,
            Next     = null,
            Previous = null,
            Results  = allResults
        };
    }

    public async Task<GeologicTileResult?> GetTileAsync(
        string scale,
        int z,
        int x,
        int y,
        CancellationToken cancellationToken)
    {
        if (!TileServices.TryGetValue(scale, out var tileService) ||
            z < MinimumTileZoom || z > tileService.MaximumZoom)
        {
            return null;
        }

        var maximumCoordinate = (1 << z) - 1;
        if (x < 0 || x > maximumCoordinate || y < 0 || y > maximumCoordinate)
        {
            return null;
        }

        var url = $"imagery/rest/services/mvCaches/{tileService.Service}/ImageServer/tile/{z}/{y}/{x}";
        using var response = await _httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is not ("image/png" or "image/jpeg"))
        {
            throw new InvalidOperationException("NGMDB returned a non-image tile response.");
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new GeologicTileResult(content, contentType);
    }

    private static string BuildPageUrl(string encodedLlb, int pageNumber)
        => $"connect/apiv1/mv/?" +
           $"llb={encodedLlb}" +
           "&include_gt=1" +
           "&ordering=-year" +
           "&format=json" +
           $"&page_size={PageSize}" +
           $"&page={pageNumber}";
}

public sealed record GeologicTileResult(byte[] Content, string ContentType);
