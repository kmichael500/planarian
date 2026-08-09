using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Planarian.Library.Options;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Shared.Repositories;

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
    private readonly ThrottleEventLogRepository _repository;

    public ThrottleEventLogService(
        ThrottleEventLogRepository repository,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ThrottleEventLogService> logger,
        MemoryCache memoryCache,
        RequestUser requestUser,
        ServerOptions serverOptions)
    {
        _repository = repository;
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
            string? ipAddress;

            try
            {
                ipAddress = RequestThrottleKeyHelper.GetClientIpAddress(httpContext);
            }
            catch (InvalidOperationException)
            {
                ipAddress = null;
            }

            await _repository.AddAsync(new ThrottleEventLog
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
            }, cancellationToken);
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

            await _repository.DeleteBeforeAsync(cutoff, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up expired throttle event logs.");
        }
    }
}
