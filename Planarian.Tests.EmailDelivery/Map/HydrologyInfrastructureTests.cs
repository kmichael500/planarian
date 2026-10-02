using System.Net;
using Planarian.Library.Exceptions;
using Planarian.Modules.Map.Controllers;
using Planarian.Modules.Map.Models;
using Planarian.Modules.Map.Services.Hydrology;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Map;

public sealed class HydrologyInfrastructureTests
{
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, 502)]
    [InlineData(HttpStatusCode.TooManyRequests, 503)]
    public async Task UpstreamHttpFailureIsTranslated(
        HttpStatusCode upstreamStatus,
        int expectedStatus)
    {
        var upstreamError = new HttpRequestException("USGS failed", null, upstreamStatus);
        var service = CreateService(new FailingProvider(upstreamError));

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            service.GetHydrologyFeaturesInBounds(
                36, 35, -85, -86, CancellationToken.None));

        Assert.Equal(expectedStatus, error.StatusCode);
    }
    [Fact]
    public async Task UpstreamTimeoutBecomesGatewayTimeout()
    {
        var service = CreateService(new FailingProvider(new TaskCanceledException("USGS timed out")));

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            service.GetHydrologyFeaturesInBounds(
                36, 35, -85, -86, CancellationToken.None));

        Assert.Equal(504, error.StatusCode);
    }

    [Fact]
    public async Task RequestCancellationIsNotReclassifiedAsUpstreamFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = CreateService(new FailingProvider(
            new OperationCanceledException(cancellation.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetHydrologyFeaturesInBounds(
                36, 35, -85, -86, cancellation.Token));
    }
    [Fact]
    public async Task SharedCacheFillSurvivesOneCallerCancellation()
    {
        using var cache = new HydrologyMemoryCache();
        using var firstCancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCalls = 0;

        async Task<string> Factory(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref factoryCalls);
            started.TrySetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return "value";
        }

        var first = cache.GetOrCreateAsync(
            "shared", TimeSpan.FromMinutes(1), Factory, _ => 1, firstCancellation.Token);
        await started.Task;
        var second = cache.GetOrCreateAsync(
            "shared", TimeSpan.FromMinutes(1), Factory, _ => 1, CancellationToken.None);

        firstCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        release.SetResult(true);

        Assert.Equal("value", await second);
        Assert.Equal(1, factoryCalls);
    }

    private static MapService CreateService(IHydrologyProvider provider) =>
        new(null!, null!, null!, [provider], null!);

    private sealed class FailingProvider(Exception exception) : IHydrologyProvider
    {
        public Task<IReadOnlyList<HydrologyFeature>> GetFeaturesInBoundsAsync(
            double north,
            double south,
            double east,
            double west,
            CancellationToken cancellationToken) =>
            Task.FromException<IReadOnlyList<HydrologyFeature>>(exception);
    }
}
