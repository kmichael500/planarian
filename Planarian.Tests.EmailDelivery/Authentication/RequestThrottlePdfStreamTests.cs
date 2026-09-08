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
    public async Task RangeRequestsInOnePdfSessionCountAsOneFileAccess()
    {
        using var db = new PlanarianDbContext(new DbContextOptionsBuilder<PlanarianDbContext>().Options);
        var requestUser = new RequestUser(db) { Id = "viewer" };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var context = CreateThrottledContext();
        var accessor = new FixedHttpContextAccessor(context);
        var service = CreateService(cache, accessor, requestUser, perFileLimit: 2);
        const string fileId = "abcdefghij";

        context.Request.Headers[RequestThrottleService.FileStreamSessionHeaderName] = Guid.NewGuid().ToString();
        context.Request.Headers[HeaderNames.Range] = "bytes=0-65535";
        for (var i = 0; i < 40; i++)
            await service.CountFileAccessAttempt(fileId);

        context.Request.Headers.Remove(HeaderNames.Range);
        await service.CountAttempt(ThrottleProfile.FileAccess, fileId);
        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.CountAttempt(ThrottleProfile.FileAccess, fileId));

        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
    }

    [Fact]
    public async Task MalformedPdfSessionHeaderCannotGroupRangeRequests()
    {
        using var db = new PlanarianDbContext(new DbContextOptionsBuilder<PlanarianDbContext>().Options);
        var requestUser = new RequestUser(db) { Id = "viewer" };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var context = CreateThrottledContext();
        var service = CreateService(cache, new FixedHttpContextAccessor(context), requestUser, perFileLimit: 2);
        const string fileId = "abcdefghij";

        context.Request.Headers[RequestThrottleService.FileStreamSessionHeaderName] = "not-a-guid";
        context.Request.Headers[HeaderNames.Range] = "bytes=0-65535";
        await service.CountFileAccessAttempt(fileId);
        await service.CountFileAccessAttempt(fileId);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.CountFileAccessAttempt(fileId));
        Assert.Equal(StatusCodes.Status429TooManyRequests, exception.StatusCode);
    }

    [Fact]
    public async Task NonRangeRequestsStillCountWhenAValidSessionHeaderIsReused()
    {
        using var db = new PlanarianDbContext(new DbContextOptionsBuilder<PlanarianDbContext>().Options);
        var requestUser = new RequestUser(db) { Id = "viewer" };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var context = CreateThrottledContext();
        var service = CreateService(cache, new FixedHttpContextAccessor(context), requestUser, perFileLimit: 2);
        const string fileId = "abcdefghij";

        context.Request.Headers[RequestThrottleService.FileStreamSessionHeaderName] = Guid.NewGuid().ToString();
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
