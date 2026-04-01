using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace Planarian.Shared.Services;

public class ApplicationEventLogService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly MemoryCache _memoryCache;
    private readonly RequestUser _requestUser;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ApplicationEventLogService(
        IServiceScopeFactory serviceScopeFactory,
        IHttpContextAccessor httpContextAccessor,
        MemoryCache memoryCache,
        RequestUser requestUser)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _httpContextAccessor = httpContextAccessor;
        _memoryCache = memoryCache;
        _requestUser = requestUser;
    }

    public async Task WriteAsync(
        ApplicationEventCategory category,
        ApplicationEventType eventType,
        string? identifier = null,
        string? message = null,
        string? dataJson = null,
        CancellationToken cancellationToken = default)
    {
        var entity = new ApplicationEventLog
        {
            Category = category,
            EventType = eventType,
            OccurredOn = DateTime.UtcNow,
            UserId = _requestUser.IsAuthenticated ? _requestUser.Id : null,
            AccountId = _requestUser.IsAuthenticated ? _requestUser.AccountId : null,
            IpAddress = RequestThrottleKeyHelper.GetClientIpAddress(_httpContextAccessor.HttpContext),
            NormalizedIdentifier = identifier,
            Path = GetRequestPath(),
            Message = message,
            DataJson = dataJson
        };

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlanarianDbContext>();
        dbContext.ApplicationEventLogs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertAsync(
        ApplicationEventCategory category,
        ApplicationEventType eventType,
        string aggregationKey,
        DateTime windowStartedOn,
        DateTime windowEndsOn,
        string? identifier = null,
        string? message = null,
        string? dataJson = null,
        CancellationToken cancellationToken = default)
    {
        var asyncLock = GetAsyncLock(aggregationKey, windowEndsOn);
        await asyncLock.Semaphore.WaitAsync(cancellationToken);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PlanarianDbContext>();
            var accountId = _requestUser.IsAuthenticated ? _requestUser.AccountId : null;
            var userId = _requestUser.IsAuthenticated ? _requestUser.Id : null;
            var ipAddress = RequestThrottleKeyHelper.GetClientIpAddress(_httpContextAccessor.HttpContext);
            var path = GetRequestPath();

            var entity = await dbContext.ApplicationEventLogs
                .FirstOrDefaultAsync(e => e.AggregationKey == aggregationKey, cancellationToken);

            if (entity == null)
            {
                entity = new ApplicationEventLog
                {
                    Category = category,
                    EventType = eventType,
                    OccurredOn = DateTime.UtcNow,
                    AttemptCount = 1,
                    WindowStartedOn = windowStartedOn,
                    WindowEndsOn = windowEndsOn,
                    UserId = userId,
                    AccountId = accountId,
                    IpAddress = ipAddress,
                    NormalizedIdentifier = identifier,
                    AggregationKey = aggregationKey,
                    Path = path,
                    Message = message,
                    DataJson = dataJson
                };

                dbContext.ApplicationEventLogs.Add(entity);
            }
            else
            {
                entity.AttemptCount += 1;
                entity.OccurredOn = DateTime.UtcNow;
                entity.Message = message;
                entity.DataJson = dataJson;
                entity.Path = path;
                entity.IpAddress = ipAddress;
                entity.UserId = userId;
                entity.AccountId = accountId;
                entity.NormalizedIdentifier = identifier;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            asyncLock.Semaphore.Release();
        }
    }

    private string GetRequestPath()
    {
        return RequestThrottleKeyHelper.GetRequestPathKey(_httpContextAccessor.HttpContext);
    }

    private AsyncLockState GetAsyncLock(string aggregationKey, DateTime windowEndsOn)
    {
        var cacheKey = $"ApplicationEventLogLock:{aggregationKey}";
        return _memoryCache.GetOrCreate(cacheKey, entry =>
        {
            // Aggregation correctness here is process-local and is acceptable for the current single-app deployment.
            entry.AbsoluteExpiration = new DateTimeOffset(windowEndsOn.AddMinutes(1));
            return new AsyncLockState();
        })!;
    }

    private sealed class AsyncLockState
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
    }
}
