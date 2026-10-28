using Npgsql;
using Planarian.Library.Options;

namespace Planarian.Modules.Account.Repositories;

/// <summary>
/// PostgreSQL session advisory leases coordinate account maintenance across API instances.
/// Shared maintenance leases protect ordinary requests/archive jobs; exclusive maintenance
/// leases protect reset and its post-commit storage cleanup. Archive jobs acquire that shared
/// maintenance lock plus a separate exclusive archive-job lock on the same session so only one
/// archive runs per account across replicas without consuming a second long-lived pool slot.
/// A lost connection releases all of its locks.
/// </summary>
public sealed class AccountMaintenanceLockRepository(ServerOptions options)
{
    private const int MaintenanceLockNamespace = 1885434480;
    private const int ArchiveJobLockNamespace = 1885434481;

    // Separate the long-lived coordination leases from the application EF pool.
    // Otherwise a burst of mutating HTTP requests could exhaust the main pool
    // before those requests can open their own working connections.
    private readonly string _connectionString = new NpgsqlConnectionStringBuilder(options.SqlConnectionString)
    {
        ApplicationName = "Planarian.AccountMaintenance",
        MaxPoolSize = 24
    }.ConnectionString;

    public Task<Lease?> TryAcquireAsync(string accountId, bool exclusive,
        CancellationToken cancellationToken = default) =>
        TryAcquireLocksAsync(accountId,
            [new AdvisoryLock(MaintenanceLockNamespace, exclusive)],
            cancellationToken);

    /// <summary>
    /// Acquires the shared maintenance lease and exclusive archive-job lease for one
    /// account on a single PostgreSQL session. Using one session avoids a pool deadlock
    /// where many archive jobs could each hold their maintenance connection while waiting
    /// for a second connection from the same bounded pool.
    /// </summary>
    public Task<Lease?> TryAcquireArchiveAsync(string accountId,
        CancellationToken cancellationToken = default) =>
        TryAcquireLocksAsync(accountId,
            [
                new AdvisoryLock(MaintenanceLockNamespace, Exclusive: false),
                new AdvisoryLock(ArchiveJobLockNamespace, Exclusive: true)
            ],
            cancellationToken);

    private async Task<Lease?> TryAcquireLocksAsync(
        string accountId, IReadOnlyList<AdvisoryLock> locks,
        CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        var acquired = new List<AdvisoryLock>(locks.Count);

        try
        {
            await connection.OpenAsync(cancellationToken);

            foreach (var advisoryLock in locks)
            {
                await using var command = new NpgsqlCommand(advisoryLock.Exclusive
                    ? "select pg_try_advisory_lock(@namespace, hashtext(@account))"
                    : "select pg_try_advisory_lock_shared(@namespace, hashtext(@account))", connection);
                command.Parameters.AddWithValue("namespace", advisoryLock.Namespace);
                command.Parameters.AddWithValue("account", accountId);

                if ((bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false))
                {
                    acquired.Add(advisoryLock);
                    continue;
                }

                await ReleaseLocksAsync(connection, accountId, acquired, CancellationToken.None);
                await connection.DisposeAsync();
                return null;
            }

            return new Lease(connection, accountId, acquired.ToArray());
        }
        catch
        {
            try
            {
                await ReleaseLocksAsync(connection, accountId, acquired, CancellationToken.None);
            }
            catch (NpgsqlException)
            {
                NpgsqlConnection.ClearPool(connection);
            }

            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task ReleaseLocksAsync(
        NpgsqlConnection connection, string accountId,
        IReadOnlyList<AdvisoryLock> locks, CancellationToken cancellationToken)
    {
        for (var index = locks.Count - 1; index >= 0; index--)
        {
            var advisoryLock = locks[index];
            await using var command = new NpgsqlCommand(advisoryLock.Exclusive
                ? "select pg_advisory_unlock(@namespace, hashtext(@account))"
                : "select pg_advisory_unlock_shared(@namespace, hashtext(@account))", connection);
            command.Parameters.AddWithValue("namespace", advisoryLock.Namespace);
            command.Parameters.AddWithValue("account", accountId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    internal readonly record struct AdvisoryLock(int Namespace, bool Exclusive);

    public sealed class Lease : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly string _accountId;
        private readonly IReadOnlyList<AdvisoryLock> _locks;

        internal Lease(
            NpgsqlConnection connection, string accountId,
            IReadOnlyList<AdvisoryLock> locks)
        {
            _connection = connection;
            _accountId = accountId;
            _locks = locks;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await ReleaseLocksAsync(_connection, _accountId, _locks, CancellationToken.None);
            }
            catch (NpgsqlException)
            {
                // A broken session has already lost its advisory locks. Drop its
                // connection from the pool instead of overriding committed work.
                NpgsqlConnection.ClearPool(_connection);
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }
}
