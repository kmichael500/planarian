using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Planarian.Modules.Map.Services.Hydrology;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Map;

public sealed class UsgsWaterDataClientTests
{
    [Fact]
    public async Task PeakSummaryUsesApiKeyAndCachesAcrossCallers()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"features":[],"links":[]}"""));
        using var httpClient = new HttpClient(handler);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(httpClient, memoryCache, "test-key");

        await Task.WhenAll(
            client.GetStreamGagePeakSummaryAsync("03425000", CancellationToken.None),
            client.GetStreamGagePeakSummaryAsync("03425000", CancellationToken.None));
        await client.GetStreamGagePeakSummaryAsync("03425000", CancellationToken.None);

        Assert.Equal(1, handler.CallCount);
        Assert.All(handler.ApiKeys, value => Assert.Equal("test-key", value));
    }

    [Fact]
    public async Task ObservationCacheSharesBucketAndTrimsToExactCallerRange()
    {
        var handler = new RecordingHandler(request =>
            request.RequestUri!.AbsolutePath.Contains("combined-metadata", StringComparison.Ordinal)
                ? JsonResponse(MetadataJson)
                : JsonResponse(ObservationsJson));
        using var httpClient = new HttpClient(handler);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(httpClient, memoryCache);

        var first = await client.GetStreamGageObservationsAsync(
            "03425000",
            DateTimeOffset.Parse("2026-09-11T21:01:00Z"),
            DateTimeOffset.Parse("2026-09-11T21:19:00Z"),
            CancellationToken.None);
        var second = await client.GetStreamGageObservationsAsync(
            "03425000",
            DateTimeOffset.Parse("2026-09-11T21:02:00Z"),
            DateTimeOffset.Parse("2026-09-11T21:18:00Z"),
            CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
        Assert.Equal(["10", "20"], first.Single().Points.Select(point => point.Value));
        Assert.Equal(["10", "20"], second.Single().Points.Select(point => point.Value));
    }

    private static UsgsWaterDataClient CreateClient(
        HttpClient httpClient,
        MemoryCache memoryCache,
        string? apiKey = null) =>
        new(
            httpClient,
            new UsgsWaterDataOptions { ApiKey = apiKey },
            new UsgsWaterDataCache(memoryCache),
            NullLogger<UsgsWaterDataClient>.Instance);

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private const string MetadataJson = """
        {
          "features": [{
            "id": "series-1",
            "properties": {
              "parameter_code": "00060",
              "unit_of_measure": "ft^3/s",
              "primary": "Primary",
              "computation_identifier": "Instantaneous",
              "begin": "2020-01-01T00:00:00Z",
              "end": "2026-12-31T23:59:59Z"
            }
          }],
          "links": []
        }
        """;

    private const string ObservationsJson = """
        {
          "features": [
            {"properties":{"time_series_id":"series-1","value":"5","time":"2026-09-11T20:59:00Z","approval_status":"Approved"}},
            {"properties":{"time_series_id":"series-1","value":"10","time":"2026-09-11T21:05:00Z","approval_status":"Approved"}},
            {"properties":{"time_series_id":"series-1","value":"20","time":"2026-09-11T21:15:00Z","approval_status":"Approved"}},
            {"properties":{"time_series_id":"series-1","value":"25","time":"2026-09-11T21:21:00Z","approval_status":"Approved"}}
          ],
          "links": []
        }
        """;

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public List<string?> ApiKeys { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            ApiKeys.Add(request.Headers.TryGetValues("X-Api-Key", out var values)
                ? values.SingleOrDefault()
                : null);
            var response = responseFactory(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
