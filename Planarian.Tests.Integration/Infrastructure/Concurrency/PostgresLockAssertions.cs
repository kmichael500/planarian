using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Planarian.Tests.Integration.Infrastructure.Concurrency;

internal static class PostgresLockAssertions
{
    public static Task<int> BackendPidAsync(Planarian.Model.Database.PlanarianDbContext db) =>
        db.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();

    public static async Task AssertBlockedAsync(PostgresTestDatabase database, string accountId, int backendPid,
        Task operation)
    {
        await using var observer = database.CreateDbContext("lock-observer", accountId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!timeout.IsCancellationRequested)
        {
            var blockerCount = await observer.Database.SqlQuery<int>(
                    $"SELECT cardinality(pg_blocking_pids({backendPid})) AS \"Value\"")
                .SingleAsync(timeout.Token);
            if (blockerCount > 0)
            {
                Assert.False(operation.IsCompleted);
                return;
            }
            await Task.Delay(10, timeout.Token);
        }
        throw new Xunit.Sdk.XunitException("The operation never entered a PostgreSQL lock wait.");
    }
}
