using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;

namespace Planarian.Modules.Caves.Revisions;

public sealed record CaveRevisionRolloutResult(DateTime RevisionHistoryStartedOn, int InitializedCaveCount,
    int BatchCount, bool AlreadyComplete);

/// <summary>
/// One-time deployment bootstrap for Cave revision history. This is deliberately separate from ordinary request
/// publication so a legacy Cave's first revision always means "revision history starts here", never "someone happened
/// to open the editor first". The command is resumable while only rollout baselines exist, and fails closed after live
/// revision publication has begun.
/// </summary>
public sealed class CaveRevisionRolloutRepository
{
    private const int BatchSize = CavePublishedSnapshotRepository.ProjectionChunkSize;
    public const string CommandLineSwitch = "--initialize-cave-revision-history";

    private readonly PlanarianDbContext _db;

    public CaveRevisionRolloutRepository(PlanarianDbContext db)
    {
        _db = db;
    }

    public async Task<CaveRevisionRolloutResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.OpenConnectionAsync(cancellationToken);
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        try
        {
            await SetRolloutAdvisoryLockAsync(connection, acquire: true, cancellationToken);
            return await InitializeLockedAsync(cancellationToken);
        }
        finally
        {
            try
            {
                await SetRolloutAdvisoryLockAsync(connection, acquire: false, CancellationToken.None);
            }
            finally
            {
                await _db.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task<CaveRevisionRolloutResult> InitializeLockedAsync(CancellationToken cancellationToken)
    {
        var uninitializedCount = await CountUninitializedCavesAsync(cancellationToken);
        var baselineTimes = await _db.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .Where(revision => revision.Source == CaveRevisionSource.SystemBaseline &&
                               revision.Operation == CaveRevisionOperation.Baseline)
            .Select(revision => revision.CreatedOn)
            .Distinct()
            .Take(2)
            .ToListAsync(cancellationToken);

        if (baselineTimes.Count > 1)
            throw new InvalidOperationException(
                "Cave revision rollout has multiple baseline timestamps. Refusing to guess the rollout boundary.");

        if (uninitializedCount == 0)
        {
            var completedOn = baselineTimes.Count == 1 ? baselineTimes[0] : DateTime.UtcNow;
            return new CaveRevisionRolloutResult(completedOn, 0, 0, true);
        }

        var hasPublishedHistory = await _db.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(revision => revision.Source != CaveRevisionSource.SystemBaseline ||
                                  revision.Operation != CaveRevisionOperation.Baseline,
                cancellationToken);
        if (hasPublishedHistory)
            throw new InvalidOperationException(
                "Cave revision history is already active while one or more Caves have no current revision. " +
                "Refusing to create a late baseline because it would not represent the rollout boundary.");

        var revisionHistoryStartedOn = baselineTimes.Count == 1 ? baselineTimes[0] : DateTime.UtcNow;
        var accountIds = await _db.Caves.IgnoreQueryFilters().AsNoTracking()
            .Where(cave => cave.CurrentRevisionId == null)
            .Select(cave => cave.AccountId)
            .Distinct()
            .OrderBy(accountId => accountId)
            .ToListAsync(cancellationToken);

        var initialized = 0;
        var batchCount = 0;
        foreach (var accountId in accountIds)
        {
            while (true)
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(
                    IsolationLevel.RepeatableRead, cancellationToken);
                try
                {
                    var caveIds = await LockNextBatchAsync(accountId, cancellationToken);
                    if (caveIds.Count == 0)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        break;
                    }

                    var snapshots = await CavePublishedSnapshotRepository.ForRevisionRollout(_db, accountId)
                        .BuildManyAsync(caveIds, cancellationToken);
                    await InsertBaselinesAndAdvancePointersAsync(
                        accountId, snapshots, revisionHistoryStartedOn, cancellationToken);

                    await transaction.CommitAsync(cancellationToken);
                    initialized += caveIds.Count;
                    batchCount++;
                    _db.ChangeTracker.Clear();
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    _db.ChangeTracker.Clear();
                    throw;
                }
            }
        }

        await EnsureCompleteAsync(cancellationToken);
        return new CaveRevisionRolloutResult(revisionHistoryStartedOn, initialized, batchCount, false);
    }

    private static async Task SetRolloutAdvisoryLockAsync(NpgsqlConnection connection, bool acquire,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = acquire
            ? "select pg_advisory_lock(hashtext(current_database()), hashtext('planarian:cave-revision-rollout'))"
            : "select pg_advisory_unlock(hashtext(current_database()), hashtext('planarian:cave-revision-rollout'))";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task EnsureCompleteAsync(CancellationToken cancellationToken = default)
    {
        var count = await CountUninitializedCavesAsync(cancellationToken);
        if (count != 0)
            throw new InvalidOperationException(
                $"Cave revision history initialization is incomplete: {count} Cave(s) have no current revision. " +
                $"Run the application with {CommandLineSwitch} after applying migrations and before serving requests.");
    }

    private Task<int> CountUninitializedCavesAsync(CancellationToken cancellationToken) =>
        _db.Caves.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(cave => cave.CurrentRevisionId == null, cancellationToken);

    private async Task<List<string>> LockNextBatchAsync(string accountId, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        var transaction = (NpgsqlTransaction)(_db.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("Cave revision rollout transaction is not active."));

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select "Id"
            from "Caves"
            where "AccountId" = @account_id and "CurrentRevisionId" is null
            order by "Id" collate "C"
            limit @batch_size
            for update
            """;
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("batch_size", BatchSize);

        var ids = new List<string>(BatchSize);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) ids.Add(reader.GetString(0));
        return ids;
    }

    private async Task InsertBaselinesAndAdvancePointersAsync(string accountId,
        IReadOnlyList<CavePublishedSnapshotV1> snapshots, DateTime revisionHistoryStartedOn,
        CancellationToken cancellationToken)
    {
        if (snapshots.Count == 0) return;

        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        var transaction = (NpgsqlTransaction)(_db.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("Cave revision rollout transaction is not active."));

        const string tempTableName = "__CaveRevisionRolloutBatch";
        await using (var create = connection.CreateCommand())
        {
            create.Transaction = transaction;
            create.CommandText = $"""
                create temporary table "{tempTableName}" (
                    "RevisionId" varchar(10) not null,
                    "CaveId" varchar(10) not null,
                    "SnapshotSchemaVersion" integer not null,
                    "SnapshotJson" jsonb not null
                ) on commit drop
                """;
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var importer = await connection.BeginBinaryImportAsync($"""
            copy "{tempTableName}" ("RevisionId", "CaveId", "SnapshotSchemaVersion", "SnapshotJson") from stdin (format binary)
            """, cancellationToken))
        {
            foreach (var snapshot in snapshots)
            {
                if (!string.Equals(snapshot.AccountId, accountId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Cave revision rollout snapshot belongs to a different account.");

                await importer.StartRowAsync(cancellationToken);
                await importer.WriteAsync(IdGenerator.Generate(), NpgsqlDbType.Varchar, cancellationToken);
                await importer.WriteAsync(snapshot.CaveId, NpgsqlDbType.Varchar, cancellationToken);
                await importer.WriteAsync(snapshot.SchemaVersion, NpgsqlDbType.Integer, cancellationToken);
                await importer.WriteAsync(CaveSnapshotJson.Serialize(snapshot), NpgsqlDbType.Jsonb, cancellationToken);
            }

            await importer.CompleteAsync(cancellationToken);
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = $"""
                insert into "CaveRevisions"
                    ("Id", "AccountId", "CaveId", "Source", "Operation", "SnapshotSchemaVersion", "SnapshotJson", "CreatedOn")
                select batch."RevisionId", @account_id, batch."CaveId", @source, @operation,
                       batch."SnapshotSchemaVersion", batch."SnapshotJson", @created_on
                from "{tempTableName}" as batch
                join "Caves" as cave
                  on cave."AccountId" = @account_id and cave."Id" = batch."CaveId"
                where cave."CurrentRevisionId" is null
                """;
            insert.Parameters.AddWithValue("account_id", accountId);
            insert.Parameters.AddWithValue("source", CaveRevisionSource.SystemBaseline.ToString());
            insert.Parameters.AddWithValue("operation", CaveRevisionOperation.Baseline.ToString());
            insert.Parameters.AddWithValue("created_on", revisionHistoryStartedOn);
            var inserted = await insert.ExecuteNonQueryAsync(cancellationToken);
            if (inserted != snapshots.Count)
                throw new InvalidOperationException(
                    $"Cave revision rollout expected to insert {snapshots.Count} baselines but inserted {inserted}.");
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = $"""
                update "Caves" as cave
                set "CurrentRevisionId" = batch."RevisionId"
                from "{tempTableName}" as batch
                where cave."AccountId" = @account_id
                  and cave."Id" = batch."CaveId"
                  and cave."CurrentRevisionId" is null
                """;
            update.Parameters.AddWithValue("account_id", accountId);
            var updated = await update.ExecuteNonQueryAsync(cancellationToken);
            if (updated != snapshots.Count)
                throw new InvalidOperationException(
                    $"Cave revision rollout expected to advance {snapshots.Count} pointers but advanced {updated}.");
        }
    }
}
