using Npgsql;
using Planarian.Library.Options;

namespace Planarian.Modules.Account.Repositories;

/// <summary>
/// PostgreSQL session advisory leases coordinate account maintenance across API instances.
/// Shared leases protect ordinary requests/archive jobs; exclusive leases protect reset
/// and its post-commit storage cleanup. A lost connection releases its locks.
/// </summary>
public sealed class AccountMaintenanceLockRepository(ServerOptions options)
{
    private const int LockNamespace = 1885434480;
    // Separate the long-lived coordination leases from the application EF pool.
    // Otherwise a burst of mutating HTTP requests could exhaust the main pool
    // before those requests can open their own working connections.
    private readonly string _connectionString = new NpgsqlConnectionStringBuilder(options.SqlConnectionString)
    {
        ApplicationName = "Planarian.AccountMaintenance",
        MaxPoolSize = 24
    }.ConnectionString;

    public async Task<Lease?> TryAcquireAsync(string accountId, bool exclusive,
        CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand(exclusive
                ? "select pg_try_advisory_lock(@namespace, hashtext(@account))"
                : "select pg_try_advisory_lock_shared(@namespace, hashtext(@account))", connection);
            command.Parameters.AddWithValue("namespace", LockNamespace);
            command.Parameters.AddWithValue("account", accountId);
            if ((bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false))
                return new Lease(connection, accountId, exclusive);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        await connection.DisposeAsync();
        return null;
    }

    public sealed class Lease(NpgsqlConnection connection, string accountId, bool exclusive) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand(exclusive
                    ? "select pg_advisory_unlock(@namespace, hashtext(@account))"
                    : "select pg_advisory_unlock_shared(@namespace, hashtext(@account))", connection);
                command.Parameters.AddWithValue("namespace", LockNamespace);
                command.Parameters.AddWithValue("account", accountId);
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch (NpgsqlException)
            {
                // A broken session has already lost its advisory lock. Drop its
                // connection from the pool instead of overriding a committed reset.
                NpgsqlConnection.ClearPool(connection);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
