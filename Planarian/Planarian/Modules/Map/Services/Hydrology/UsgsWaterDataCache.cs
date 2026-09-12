using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Planarian.Modules.Map.Services.Hydrology;

public sealed class UsgsWaterDataCache(MemoryCache cache)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<object>>> _inFlight = new();

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan lifetime,
        Func<Task<T>> factory,
        CancellationToken cancellationToken)
        where T : notnull
    {
        if (cache.TryGetValue<T>(key, out var cached)) return cached!;

        Lazy<Task<object>>? candidate = null;
        candidate = new Lazy<Task<object>>(async () =>
        {
            try
            {
                var value = await factory();
                cache.Set(key, value, lifetime);
                return value;
            }
            finally
            {
                _inFlight.TryRemove(key, out _);
            }
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        var shared = _inFlight.GetOrAdd(key, candidate);
        if (ReferenceEquals(shared, candidate) && cache.TryGetValue<T>(key, out cached))
        {
            _inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<object>>>(key, candidate));
            return cached!;
        }

        var result = await shared.Value.WaitAsync(cancellationToken);
        return (T)result;
    }
}
