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
using Planarian.Shared.Repositories;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.Unit.Authentication;

public sealed class RequestThrottleServiceTests
{
    [Fact]
    public async Task PdfRangeRequestsInOneStreamSessionDoNotConsumeSeparateFileAccessAttempts()
    {
        using var db = new PlanarianDbContext(new DbContextOptionsBuilder<PlanarianDbContext>().Options);
        var requestUser = new RequestUser(db) { Id = "viewer" };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var httpContext = CreateThrottledHttpContext();
        var accessor = new FixedHttpContextAccessor(httpContext);
        var service = CreateService(cache, accessor, requestUser);
        const string fileId = "abcdefghij";
        httpContext.Request.Headers[RequestThrottleService.FileStreamSessionHeaderName] = Guid.NewGuid().ToString();

        await service.CountFileAccessAttempt(fileId);

        httpContext.Request.Headers[HeaderNames.Range] = "bytes=0-65535";
        for (var i = 0; i < 50; i++)
            await service.CountFileAccessAttempt(fileId);

        httpContext.Request.Headers.Remove(HeaderNames.Range);
        for (var i = 0; i < 2; i++)
            await service.CountAttempt(ThrottleProfile.FileAccess, fileId);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.CountAttempt(ThrottleProfile.FileAccess, fileId));
        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
    }

    [Fact]
    public async Task ConcurrentFirstRangesInOneStreamSessionCountOnlyOnce()
    {
        using var db = new PlanarianDbContext(new DbContextOptionsBuilder<PlanarianDbContext>().Options);
        var requestUser = new RequestUser(db) { Id = "viewer" };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var httpContext = CreateThrottledHttpContext();
        var accessor = new FixedHttpContextAccessor(httpContext);
        var service = CreateService(cache, accessor, requestUser);
        const string fileId = "abcdefghij";

        for (var i = 0; i < 2; i++)
            await service.CountAttempt(ThrottleProfile.FileAccess, fileId);

        var sessionId = Guid.NewGuid().ToString();
        using var ready = new CountdownEvent(10);
        using var start = new ManualResetEventSlim(false);
        var concurrentRequests = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            var requestContext = CreateThrottledHttpContext();
            requestContext.Request.Headers[RequestThrottleService.FileStreamSessionHeaderName] = sessionId;
            requestContext.Request.Headers[HeaderNames.Range] = "bytes=0-65535";
            var requestAccessor = new FixedHttpContextAccessor(requestContext);
            var requestService = CreateService(cache, requestAccessor, requestUser);
            ready.Signal();
            start.Wait();
            await requestService.CountFileAccessAttempt(fileId);
        })).ToArray();

        ready.Wait();
        start.Set();
        await Task.WhenAll(concurrentRequests);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.CountAttempt(ThrottleProfile.FileAccess, fileId));
        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
    }

    [Fact]
    public async Task MalformedStreamSessionHeaderDoesNotGroupRangeRequests()
    {
        using var db = new PlanarianDbContext(new DbContextOptionsBuilder<PlanarianDbContext>().Options);
        var requestUser = new RequestUser(db) { Id = "viewer" };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var httpContext = CreateThrottledHttpContext();
        var accessor = new FixedHttpContextAccessor(httpContext);
        var service = CreateService(cache, accessor, requestUser);
        const string fileId = "abcdefghij";
        httpContext.Request.Headers[RequestThrottleService.FileStreamSessionHeaderName] = "not-a-guid";
        httpContext.Request.Headers[HeaderNames.Range] = "bytes=0-65535";

        for (var i = 0; i < 3; i++)
            await service.CountFileAccessAttempt(fileId);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.CountFileAccessAttempt(fileId));
        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
    }

    [Fact]
    public async Task FirstRangeRequestInAStreamSessionStillCountsAsFileAccess()
    {
        using var db = new PlanarianDbContext(new DbContextOptionsBuilder<PlanarianDbContext>().Options);
        var requestUser = new RequestUser(db) { Id = "viewer" };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var httpContext = CreateThrottledHttpContext();
        var accessor = new FixedHttpContextAccessor(httpContext);
        var service = CreateService(cache, accessor, requestUser);
        const string fileId = "abcdefghij";
        httpContext.Request.Headers[RequestThrottleService.FileStreamSessionHeaderName] = Guid.NewGuid().ToString();
        httpContext.Request.Headers[HeaderNames.Range] = "bytes=0-65535";

        await service.CountFileAccessAttempt(fileId);
        for (var i = 0; i < 2; i++)
            await service.CountAttempt(ThrottleProfile.FileAccess, fileId);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.CountAttempt(ThrottleProfile.FileAccess, fileId));
        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
    }

    private static DefaultHttpContext CreateThrottledHttpContext()
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
        RequestUser requestUser)
    {
        var eventLog = new ThrottleEventLogService(
            new ThrottleEventLogRepository(new ThrowingDbContextFactory()),
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
                FileAccessPerUserPerFileLimit = 3,
                FileAccessWindowMinutes = 10
            },
            accessor,
            requestUser,
            eventLog);
    }

    private sealed class FixedHttpContextAccessor : IHttpContextAccessor
    {
        public FixedHttpContextAccessor(HttpContext httpContext) => HttpContext = httpContext;
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<PlanarianDbContext>
    {
        public PlanarianDbContext CreateDbContext() =>
            throw new InvalidOperationException("Throttle logging should not require a database in this unit test.");

        public Task<PlanarianDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<PlanarianDbContext>(
                new InvalidOperationException("Throttle logging should not require a database in this unit test."));
    }
}
