using System.Text.Encodings.Web;
using System.Text.Json;

namespace Planarian.Modules.Map.Services;

public class GeologicMapHttpClient
{
    private const int PageSize = 50;
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

    private static string BuildPageUrl(string encodedLlb, int pageNumber)
        => $"connect/apiv1/mv/?" +
           $"llb={encodedLlb}" +
           "&include_gt=1" +
           "&ordering=-year" +
           "&format=json" +
           $"&page_size={PageSize}" +
           $"&page={pageNumber}";
}
