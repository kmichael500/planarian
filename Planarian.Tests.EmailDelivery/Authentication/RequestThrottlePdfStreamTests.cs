using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Net.Http.Headers;
using Planarian.Library.Exceptions;
using Planarian.Library.Options;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Shared.Attributes;
using Planarian.Shared.Options;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Authentication;

public sealed class RequestThrottlePdfStreamTests
{
    [Fact]
    public async Task ConcurrentRangeRequestsInOnePdfSessionCountAsOneFileAccess()
    {
        using var db = new PlanarianDbContext(new DbContextOptionsBuilder<PlanarianDbContext>().Options);
        var requestUser = new RequestUser(db) { Id = "viewer" };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        const string fileId = "abcdefghij";
        var sessionId = Guid.NewGuid().ToString();
        var services = Enumerable.Range(0, 40)
            .Select(_ =>
            {
                var context = CreateThrottledContext();
                context.Request.QueryString = QueryString.Create(
                    RequestThrottleService.FileStreamSessionQueryParameterName,
                    sessionId);
                context.Request.Headers[HeaderNames.Range] = "bytes=0-65535";
                return CreateService(
                    cache,
                    new FixedHttpContextAccessor(context),
                    requestUser,
                    perFileLimit: 2);
            })
            .ToArray();

        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var rangeRequests = services
            .Select(async service =>
            {
                await start.Task;
                await service.CountFileAccessAttempt(fileId);
            })
            .ToArray();

        start.SetResult(true);
        await Task.WhenAll(rangeRequests);

        var verificationContext = CreateThrottledContext();
        var verificationService = CreateService(
            cache,
            new FixedHttpContextAccessor(verificationContext),
            requestUser,
            perFileLimit: 2);
        await verificationService.CountAttempt(ThrottleProfile.FileAccess, fileId);
        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            verificationService.CountAttempt(ThrottleProfile.FileAccess, fileId));

        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
    }

    [Fact]
    public async Task MalformedPdfSessionCannotGroupRangeRequests()
    {
        using var db = new PlanarianDbContext(new DbContextOptionsBuilder<PlanarianDbContext>().Options);
        var requestUser = new RequestUser(db) { Id = "viewer" };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var context = CreateThrottledContext();
        var service = CreateService(cache, new FixedHttpContextAccessor(context), requestUser, perFileLimit: 2);
        const string fileId = "abcdefghij";

        context.Request.QueryString = QueryString.Create(
            RequestThrottleService.FileStreamSessionQueryParameterName,
            "not-a-guid");
        context.Request.Headers[HeaderNames.Range] = "bytes=0-65535";
        await service.CountFileAccessAttempt(fileId);
        await service.CountFileAccessAttempt(fileId);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.CountFileAccessAttempt(fileId));
        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
    }

    [Fact]
    public async Task NonRangeRequestsStillCountWhenAValidSessionIsReused()
    {
        using var db = new PlanarianDbContext(new DbContextOptionsBuilder<PlanarianDbContext>().Options);
        var requestUser = new RequestUser(db) { Id = "viewer" };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var context = CreateThrottledContext();
        var service = CreateService(cache, new FixedHttpContextAccessor(context), requestUser, perFileLimit: 2);
        const string fileId = "abcdefghij";

        context.Request.QueryString = QueryString.Create(
            RequestThrottleService.FileStreamSessionQueryParameterName,
            Guid.NewGuid().ToString());
        await service.CountFileAccessAttempt(fileId);
        await service.CountFileAccessAttempt(fileId);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.CountFileAccessAttempt(fileId));
        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
    }

    [Fact]
    public async Task FileAccessCanAlsoUseEndpointTransportRateLimiting()
    {
        using var db = new PlanarianDbContext(new DbContextOptionsBuilder<PlanarianDbContext>().Options);
        var requestUser = new RequestUser(db) { Id = "viewer" };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(
            requestDelegate: null,
            metadata: new EndpointMetadataCollection(new ThrottleAttribute { RequestsPerMinute = 1200 }),
            displayName: "file-access-with-transport-limit"));
        var service = CreateService(cache, new FixedHttpContextAccessor(context), requestUser, perFileLimit: 2);

        await service.CountFileAccessAttempt("abcdefghij");
    }

    private static DefaultHttpContext CreateThrottledContext()
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(
            requestDelegate: null,
            metadata: new EndpointMetadataCollection(new ThrottleAttribute()),
            displayName: "file-access-test"));
        return context;
    }

    private static RequestThrottleService CreateService(
        MemoryCache cache,
        IHttpContextAccessor accessor,
        RequestUser requestUser,
        int perFileLimit)
    {
        var eventLog = new ThrottleEventLogService(
            new ThrowingDbContextFactory(),
            accessor,
            NullLogger<ThrottleEventLogService>.Instance,
            cache,
            requestUser,
            new ServerOptions());
        return new RequestThrottleService(
            cache,
            new RequestThrottleOptions
            {
                FileAccessPerUserLimit = 100,
                FileAccessPerUserPerFileLimit = perFileLimit,
                FileAccessWindowMinutes = 10
            },
            accessor,
            requestUser,
            eventLog);
    }

    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public FixedHttpContextAccessor(HttpContext context) => HttpContext = context;
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<PlanarianDbContext>
    {
        public PlanarianDbContext CreateDbContext() =>
            throw new InvalidOperationException("Throttle logging is not part of this unit test.");

        public Task<PlanarianDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<PlanarianDbContext>(
                new InvalidOperationException("Throttle logging is not part of this unit test."));
    }
}
