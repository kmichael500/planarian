using System.Net;
using System.Net.Http.Headers;
using Planarian.Modules.Map.Services;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Map;

public sealed class GeologicMapHttpClientTests
{
    [Fact]
    public async Task GetTileAsyncMaps63KToCurrentNgmdbService()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3])
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return response;
        });
        using var httpClient = new HttpClient(handler);
        var client = new GeologicMapHttpClient(httpClient);

        var result = await client.GetTileAsync("63K", 10, 265, 401, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal([1, 2, 3], result.Content);
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(
            "https://ngmdb.usgs.gov/imagery/rest/services/mvCaches/mvCache63K/ImageServer/tile/10/401/265",
            handler.RequestUri?.AbsoluteUri);
    }

    [Theory]
    [InlineData("../../anything", 10, 265, 401)]
    [InlineData("24K", 3, 1, 1)]
    [InlineData("500K", 13, 1, 1)]
    [InlineData("24K", 10, 1024, 401)]
    public async Task GetTileAsyncRejectsInvalidRequestsWithoutCallingUpstream(
        string scale,
        int z,
        int x,
        int y)
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            throw new InvalidOperationException("The upstream service should not be called."));
        using var httpClient = new HttpClient(handler);
        var client = new GeologicMapHttpClient(httpClient);

        var result = await client.GetTileAsync(scale, z, x, y, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, handler.CallCount);
    }

    private sealed class RecordingHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(request));
        }
    }
}
