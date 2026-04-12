using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Planarian.Library.Options;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;

namespace Planarian.Shared.Services;

public class ThrottleEventLogService
{
    private const string CleanupCacheKey = "ThrottleEventLogService:Cleanup";
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromDays(1);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ThrottleEventLogService> _logger;
    private readonly MemoryCache _memoryCache;
    private readonly RequestUser _requestUser;
    private readonly TimeSpan _retentionWindow;
    private readonly IDbContextFactory<PlanarianDbContext> _dbContextFactory;

    public ThrottleEventLogService(
        IDbContextFactory<PlanarianDbContext> dbContextFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ThrottleEventLogService> logger,
        MemoryCache memoryCache,
        RequestUser requestUser,
        ServerOptions serverOptions)
    {
        _dbContextFactory = dbContextFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _memoryCache = memoryCache;
        _requestUser = requestUser;
        _retentionWindow = TimeSpan.FromDays(Math.Max(1, serverOptions.ThrottleEventLogRetentionDays));
    }

    public async Task TryWriteAsync(
        ThrottleProfile operationName,
        RequestThrottleKeyType limiterKeyType,
        string? normalizedIdentifier,
        int limit,
        TimeSpan window,
        int retryAfterSeconds,
        HttpContext? httpContext = null,
        CancellationToken cancellationToken = default)
    {
        httpContext ??= _httpContextAccessor.HttpContext;

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            string? ipAddress;

            try
            {
                ipAddress = RequestThrottleKeyHelper.GetClientIpAddress(httpContext);
            }
            catch (InvalidOperationException)
            {
                ipAddress = null;
            }

            dbContext.ThrottleEventLogs.Add(new ThrottleEventLog
            {
                OperationName = operationName,
                LimiterKeyType = limiterKeyType,
                Path = RequestThrottleKeyHelper.GetRequestPathKey(httpContext),
                UserId = _requestUser.IsAuthenticated ? _requestUser.Id : null,
                AccountId = _requestUser.IsAuthenticated ? _requestUser.AccountId : null,
                IpAddress = ipAddress,
                NormalizedIdentifier = string.IsNullOrWhiteSpace(normalizedIdentifier) ? null : normalizedIdentifier,
                Limit = limit,
                WindowSeconds = Math.Max(1, (int)Math.Ceiling(window.TotalSeconds)),
                RetryAfterSeconds = retryAfterSeconds,
                OccurredOn = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to write throttle event log for {Operation} on {RequestPath}.",
                operationName,
                RequestThrottleKeyHelper.GetRequestPathKey(httpContext));
            return;
        }

        await TryCleanupAsync(cancellationToken);
    }

    private async Task TryCleanupAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(CleanupCacheKey, out _))
        {
            return;
        }

        _memoryCache.Set(CleanupCacheKey, true, new DateTimeOffset(DateTime.UtcNow.Add(CleanupInterval)));

        try
        {
            var cutoff = DateTime.UtcNow.Subtract(_retentionWindow);

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            await dbContext.ThrottleEventLogs
                .Where(e => e.OccurredOn < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up expired throttle event logs.");
        }
    }
}
