using System.Net;
using System.Text;
using Planarian.Modules.Map.Services.Hydrology;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Map;

public sealed class Usgs3DhpHydrologyProviderTests
{
    [Fact]
    public async Task BoundsCacheReusesGridQueryAndTrimsToCallerBounds()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var cache = new HydrologyMemoryCache();
        var provider = new Usgs3DhpHydrologyProvider(httpClient, cache);

        var first = await provider.GetFeaturesInBoundsAsync(
            35.04, 35.01, -86.01, -86.04, CancellationToken.None);
        var second = await provider.GetFeaturesInBoundsAsync(
            35.045, 35.03, -86.005, -86.045, CancellationToken.None);

        Assert.Single(first);
        Assert.Empty(second);
        Assert.Equal(1, handler.CallCount);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            const string json = """
                {
                  "features": [{
                    "attributes": {
                      "id3dhp": "feature-1",
                      "gnisidlabel": "Spring A",
                      "featuretype": 7,
                      "featuretypelabel": "Spring"
                    },
                    "geometry": {"x": -86.02, "y": 35.02}
                  }],
                  "exceededTransferLimit": false
                }
                """;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                RequestMessage = request
            };
            return Task.FromResult(response);
        }
    }
}
