using System.Net;
using System.Text;
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
        using var cache = new HydrologyMemoryCache();
        var client = CreateClient(httpClient, cache, "test-key");

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
        using var cache = new HydrologyMemoryCache();
        var client = CreateClient(httpClient, cache);

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

    [Fact]
    public async Task NearbyGagesMapsMetadataAndObservationSummary()
    {
        var handler = new RecordingHandler(request =>
            request.RequestUri!.AbsolutePath.Contains("combined-metadata", StringComparison.Ordinal)
                ? JsonResponse(NearbyMetadataJson)
                : JsonResponse(NearbyObservationsJson));
        using var httpClient = new HttpClient(handler);
        using var cache = new HydrologyMemoryCache();
        var client = CreateClient(httpClient, cache);

        var gages = await client.GetNearbyStreamGagesAsync(
            [new("cave-1", "Test Cave", 35.0, -86.0)],
            25,
            CancellationToken.None);

        var gage = Assert.Single(gages);
        Assert.Equal("03425000", gage.SiteCode);
        Assert.Equal("Test Cave", gage.NearestOriginName);
        Assert.Equal(["10", "20"], gage.Parameters.Single().Points.Select(point => point.Value));
    }

    [Fact]
    public async Task BoundsCacheReusesGridAndTrimsToExactViewport()
    {
        var handler = new RecordingHandler(_ => JsonResponse(BoundsMetadataJson));
        using var httpClient = new HttpClient(handler);
        using var cache = new HydrologyMemoryCache();
        var client = CreateClient(httpClient, cache);

        var first = await client.GetStreamGagesInBoundsAsync(
            35.04, 35.00, -85.96, -86.00, CancellationToken.None);
        var second = await client.GetStreamGagesInBoundsAsync(
            35.05, 35.01, -85.95, -85.99, CancellationToken.None);

        Assert.Equal(1, handler.CallCount);
        Assert.Equal("USGS-1", Assert.Single(first).Id);
        Assert.Equal("USGS-2", Assert.Single(second).Id);
    }

    private static UsgsWaterDataClient CreateClient(
        HttpClient httpClient,
        HydrologyMemoryCache cache,
        string? apiKey = null) =>
        new(
            httpClient,
            new UsgsWaterDataOptions { ApiKey = apiKey },
            cache,
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

    private const string NearbyMetadataJson = """
        {
          "features": [{
            "id": "series-nearby",
            "geometry": {"coordinates": [-86.01, 35.01]},
            "properties": {
              "monitoring_location_id": "USGS-03425000",
              "monitoring_location_number": "03425000",
              "monitoring_location_name": "Test Gage",
              "parameter_code": "00060",
              "unit_of_measure": "ft^3/s",
              "primary": "Primary",
              "computation_identifier": "Instantaneous",
              "begin": "2020-01-01T00:00:00Z",
              "end": "2099-12-31T23:59:59Z"
            }
          }],
          "links": []
        }
        """;

    private const string NearbyObservationsJson = """
        {
          "features": [
            {"properties":{"time_series_id":"series-nearby","value":"10","time":"2026-09-13T12:00:00Z","approval_status":"Approved"}},
            {"properties":{"time_series_id":"series-nearby","value":"20","time":"2026-09-13T18:00:00Z","approval_status":"Approved"}}
          ],
          "links": []
        }
        """;

    private const string BoundsMetadataJson = """
        {
          "features": [
            {
              "geometry": {"coordinates": [-85.995, 35.005]},
              "properties": {
                "monitoring_location_id": "USGS-1",
                "monitoring_location_number": "1",
                "monitoring_location_name": "First",
                "computation_identifier": "Instantaneous",
                "end": "2099-12-31T23:59:59Z"
              }
            },
            {
              "geometry": {"coordinates": [-85.955, 35.045]},
              "properties": {
                "monitoring_location_id": "USGS-2",
                "monitoring_location_number": "2",
                "monitoring_location_name": "Second",
                "computation_identifier": "Instantaneous",
                "end": "2099-12-31T23:59:59Z"
              }
            }
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
