using System.Collections.Concurrent;
using Planarian.Library.Exceptions;
using Planarian.Shared.Options;

namespace Planarian.Modules.Account.Services;

public class ImportUploadAdmissionService
{
    private readonly int _retryAfterSeconds;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentDictionary<string, int> _activeRequests = new();

    public ImportUploadAdmissionService(RequestThrottleOptions options)
    {
        var maxConcurrency = Math.Max(1, options.ImportFileConcurrentUploadsPerInstance);
        _retryAfterSeconds = Math.Max(1, options.ImportFileBusyRetryAfterSeconds);
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public async Task<IAsyncDisposable> AcquireAsync(string requestId, CancellationToken cancellationToken)
    {
        if (!await _semaphore.WaitAsync(0, cancellationToken))
        {
            throw ApiExceptionDictionary.TooManyRequests(
                "Too many import file uploads are currently being processed. Please retry shortly.",
                _retryAfterSeconds);
        }

        _activeRequests[requestId] = Environment.TickCount;
        return new Releaser(_semaphore, _activeRequests, requestId);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentDictionary<string, int> _activeRequests;
        private readonly string _requestId;
        private int _disposed;

        public Releaser(
            SemaphoreSlim semaphore,
            ConcurrentDictionary<string, int> activeRequests,
            string requestId)
        {
            _semaphore = semaphore;
            _activeRequests = activeRequests;
            _requestId = requestId;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return ValueTask.CompletedTask;
            }

            _activeRequests.TryRemove(_requestId, out _);
            _semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
