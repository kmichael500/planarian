using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Planarian.Modules.Map.Services.Hydrology;

public sealed class HydrologyMemoryCache : IDisposable
{
    private const long SizeLimit = 50_000;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions
    {
        SizeLimit = SizeLimit
    });
    private readonly ConcurrentDictionary<string, InFlightOperation> _inFlight = new();

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan lifetime,
        Func<CancellationToken, Task<T>> factory,
        Func<T, long> getSize,
        CancellationToken cancellationToken)
        where T : notnull
    {
        if (_cache.TryGetValue<T>(key, out var cached)) return cached!;

        InFlightOperation? candidate = null;
        candidate = new InFlightOperation(async sharedCancellation =>
        {
            try
            {
                var value = await factory(sharedCancellation);
                var size = Math.Clamp(getSize(value), 1, SizeLimit);
                _cache.Set(
                    key,
                    value,
                    new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(lifetime)
                        .SetSize(size));
                return value;
            }
            finally
            {
                _inFlight.TryRemove(
                    new KeyValuePair<string, InFlightOperation>(key, candidate!));
            }
        });

        var shared = _inFlight.GetOrAdd(key, candidate);
        if (!ReferenceEquals(shared, candidate)) candidate.Dispose();

        shared.AddWaiter();
        try
        {
            if (_cache.TryGetValue<T>(key, out cached))
            {
                if (ReferenceEquals(shared, candidate) &&
                    _inFlight.TryRemove(new KeyValuePair<string, InFlightOperation>(key, candidate)))
                    candidate.Dispose();
                return cached!;
            }

            var result = await shared.Task.Value.WaitAsync(cancellationToken);
            return (T)result;
        }
        finally
        {
            shared.RemoveWaiter();
        }
    }

    public void Dispose()
    {
        foreach (var operation in _inFlight.Values) operation.Cancel();
        _cache.Dispose();
    }

    private sealed class InFlightOperation : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        private int _waiterCount;
        private int _completed;
        private int _disposed;

        public InFlightOperation(Func<CancellationToken, Task<object>> factory)
        {
            Task = new Lazy<Task<object>>(() =>
            {
                var task = factory(_cancellation.Token);
                _ = task.ContinueWith(
                    _ => Complete(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return task;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public Lazy<Task<object>> Task { get; }

        public void AddWaiter() => Interlocked.Increment(ref _waiterCount);

        public void RemoveWaiter()
        {
            if (Interlocked.Decrement(ref _waiterCount) != 0) return;

            if (Task.IsValueCreated && !Task.Value.IsCompleted) Cancel();
            if (Volatile.Read(ref _completed) == 1) Dispose();
        }

        public void Complete()
        {
            Interlocked.Exchange(ref _completed, 1);
            if (Volatile.Read(ref _waiterCount) == 0) Dispose();
        }

        public void Cancel()
        {
            if (!_cancellation.IsCancellationRequested) _cancellation.Cancel();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) _cancellation.Dispose();
        }
    }
}
