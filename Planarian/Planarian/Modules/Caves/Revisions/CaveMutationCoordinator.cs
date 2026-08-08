using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Revisions;

public sealed record CaveMutationResult(string CaveId, string? RevisionId, bool CreatedRevision);

/// <summary>
/// Captures the revision pointer/history state and the live published snapshot
/// before a caller-owned transaction mutates a Cave aggregate. The previous
/// revision snapshot is intentionally kept separate from the live snapshot:
/// shared reference metadata may have changed since the prior Cave revision,
/// and the next legitimate Cave publication must make those effective changes
/// observable without synthesizing revision fan-out at taxonomy-edit time.
/// </summary>
public sealed record CaveMutationPreparation(
    string CaveId,
    CaveRevision PreviousRevision,
    CavePublishedSnapshotV1 PreviousRevisionSnapshot,
    CavePublishedSnapshotV1 LiveSnapshotBeforeMutation);

public sealed class CaveMutationCoordinator
{
    private readonly PlanarianDbContext _db;
    private readonly CavePublishedSnapshotReader _snapshots;
    private readonly CaveRevisionDiffService _diff = new();
    private readonly AccountExecutionScope _scope;

    public CaveMutationCoordinator(PlanarianDbContext db, RequestUser requestUser, CavePublishedSnapshotReader snapshots)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
        _snapshots = snapshots;
    }

    /// <summary>
    /// Convenience path for a small synchronous Cave mutation. The coordinator
    /// owns the transaction and delegates revision publication to the same
    /// prepare/publish primitives used by larger service transactions.
    /// </summary>
    public async Task<CaveMutationResult> PublishExistingAsync(string caveId, string? expectedRevisionId,
        CaveRevisionSource source, CaveRevisionOperation operation, Action<Cave> write,
        string? changeRequestId = null, string? importBatchId = null, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var preparation = await PrepareExistingAsync(caveId, expectedRevisionId, cancellationToken);
            var cave = await GetOwnedCaveAsync(caveId, cancellationToken);
            write(cave);
            await _db.SaveChangesAsync(cancellationToken);
            var result = await PublishPreparedAsync(preparation, source, operation, changeRequestId, importBatchId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Creates a new Cave and its first accepted revision atomically.
    /// </summary>
    public async Task<CaveMutationResult> PublishNewAsync(Cave cave, CaveRevisionSource source,
        CaveRevisionOperation operation = CaveRevisionOperation.Create, string? changeRequestId = null,
        string? importBatchId = null, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(cave.AccountId) && cave.AccountId != _scope.AccountId)
                throw new InvalidOperationException("Cave account does not match the current account.");
            cave.AccountId = _scope.AccountId;
            _db.Caves.Add(cave);
            await _db.SaveChangesAsync(cancellationToken);
            var result = await PublishPersistedNewAsync(cave.Id, source, operation, changeRequestId, importBatchId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Prepares an existing Cave for a larger mutation whose transaction is
    /// already owned by the caller (manager edit, file publication, hard delete,
    /// approval, etc.). Baseline establishment is therefore part of the same
    /// atomic unit as the eventual write.
    /// </summary>
    public async Task<CaveMutationPreparation> PrepareExistingAsync(string caveId, string? expectedRevisionId = null,
        CancellationToken cancellationToken = default)
    {
        RequireCallerTransaction();
        var cave = await GetOwnedCaveAsync(caveId, cancellationToken);
        if (expectedRevisionId is not null && cave.CurrentRevisionId != expectedRevisionId)
            throw new CaveRevisionConflictException(caveId, expectedRevisionId, cave.CurrentRevisionId);

        var previous = await EstablishBaselineIfNeededAsync(cave, cancellationToken);
        var live = await _snapshots.BuildAsync(caveId, cancellationToken);
        return new CaveMutationPreparation(caveId, previous.Revision, previous.Snapshot, live);
    }

    /// <summary>
    /// Publishes the post-mutation snapshot while the caller's transaction is
    /// still active. No revision is created for a semantic no-op. The current
    /// pointer must still match the preparation so stale/reentrant publication
    /// fails instead of silently forking history.
    /// </summary>
    public async Task<CaveMutationResult> PublishPreparedAsync(CaveMutationPreparation preparation,
        CaveRevisionSource source, CaveRevisionOperation operation, string? changeRequestId = null,
        string? importBatchId = null, CancellationToken cancellationToken = default)
    {
        RequireCallerTransaction();
        var cave = await GetOwnedCaveAsync(preparation.CaveId, cancellationToken);
        if (cave.CurrentRevisionId != preparation.PreviousRevision.Id)
            throw new CaveRevisionConflictException(preparation.CaveId, preparation.PreviousRevision.Id,
                cave.CurrentRevisionId);

        var current = await _snapshots.BuildAsync(preparation.CaveId, cancellationToken);
        if (_diff.IsSemanticEqual(preparation.PreviousRevisionSnapshot, current))
            return new CaveMutationResult(preparation.CaveId, cave.CurrentRevisionId, false);

        var revision = NewRevision(cave.AccountId, cave.Id, preparation.PreviousRevision, current, source, operation,
            changeRequestId, importBatchId);
        _db.CaveRevisions.Add(revision);
        await _db.SaveChangesAsync(cancellationToken);
        cave.CurrentRevisionId = revision.Id;
        await _db.SaveChangesAsync(cancellationToken);
        return new CaveMutationResult(cave.Id, revision.Id, true);
    }

    /// <summary>
    /// Publishes the first revision for a Cave that the caller has already
    /// persisted inside its active transaction.
    /// </summary>
    public async Task<CaveMutationResult> PublishPersistedNewAsync(string caveId, CaveRevisionSource source,
        CaveRevisionOperation operation = CaveRevisionOperation.Create, string? changeRequestId = null,
        string? importBatchId = null, CancellationToken cancellationToken = default)
    {
        RequireCallerTransaction();
        var cave = await GetOwnedCaveAsync(caveId, cancellationToken);
        if (cave.CurrentRevisionId is not null)
            throw new CaveRevisionConflictException(caveId, null, cave.CurrentRevisionId);

        var snapshot = await _snapshots.BuildAsync(cave.Id, cancellationToken);
        var revision = NewRevision(cave.AccountId, cave.Id, null, snapshot, source, operation, changeRequestId,
            importBatchId);
        _db.CaveRevisions.Add(revision);
        await _db.SaveChangesAsync(cancellationToken);
        cave.CurrentRevisionId = revision.Id;
        await _db.SaveChangesAsync(cancellationToken);
        return new CaveMutationResult(cave.Id, revision.Id, true);
    }

    /// <summary>
    /// Writes a final tombstone revision after the caller has removed the Cave
    /// aggregate but before its transaction commits. The tombstone stores the
    /// live snapshot captured immediately before deletion, including effective
    /// shared-reference labels, and remains linked to the prior revision.
    /// </summary>
    public async Task<CaveMutationResult> PublishPreparedDeleteAsync(CaveMutationPreparation preparation,
        CaveRevisionSource source, string? changeRequestId = null, string? importBatchId = null,
        CancellationToken cancellationToken = default)
    {
        RequireCallerTransaction();
        var stillExists = await _db.Caves.IgnoreQueryFilters()
            .AnyAsync(c => c.Id == preparation.CaveId && c.AccountId == _scope.AccountId, cancellationToken);
        if (stillExists)
            throw new InvalidOperationException("Cave must be removed before publishing its delete revision.");

        var revision = NewRevision(_scope.AccountId, preparation.CaveId, preparation.PreviousRevision,
            preparation.LiveSnapshotBeforeMutation, source, CaveRevisionOperation.Delete, changeRequestId,
            importBatchId);
        _db.CaveRevisions.Add(revision);
        await _db.SaveChangesAsync(cancellationToken);
        return new CaveMutationResult(preparation.CaveId, revision.Id, true);
    }

    private async Task<Cave> GetOwnedCaveAsync(string caveId, CancellationToken cancellationToken) =>
        await _db.Caves.IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.Id == caveId && c.AccountId == _scope.AccountId, cancellationToken)
        ?? throw new InvalidOperationException("Cave is not owned by the current account.");

    private async Task<(CaveRevision Revision, CavePublishedSnapshotV1 Snapshot)> EstablishBaselineIfNeededAsync(
        Cave cave, CancellationToken cancellationToken)
    {
        if (cave.CurrentRevisionId is not null)
        {
            var existing = await _db.CaveRevisions
                .SingleAsync(r => r.Id == cave.CurrentRevisionId && r.AccountId == _scope.AccountId,
                    cancellationToken);
            return (existing, CaveSnapshotJson.Deserialize(existing.SnapshotJson, existing.SnapshotSchemaVersion));
        }

        var snapshot = await _snapshots.BuildAsync(cave.Id, cancellationToken);
        var baseline = NewRevision(cave.AccountId, cave.Id, null, snapshot, CaveRevisionSource.SystemBaseline,
            CaveRevisionOperation.Create, null, null);
        _db.CaveRevisions.Add(baseline);
        await _db.SaveChangesAsync(cancellationToken);
        cave.CurrentRevisionId = baseline.Id;
        await _db.SaveChangesAsync(cancellationToken);
        return (baseline, snapshot);
    }

    private void RequireCallerTransaction()
    {
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("This Cave revision operation requires an active database transaction.");
    }

    private static CaveRevision NewRevision(string accountId, string caveId, CaveRevision? previous,
        CavePublishedSnapshotV1 snapshot, CaveRevisionSource source, CaveRevisionOperation operation,
        string? changeRequestId, string? importBatchId) => new()
    {
        AccountId = accountId,
        CaveId = caveId,
        PreviousRevisionId = previous?.Id,
        Source = source,
        Operation = operation,
        SnapshotSchemaVersion = 1,
        SnapshotJson = CaveSnapshotJson.Serialize(snapshot),
        ChangeRequestId = changeRequestId,
        ImportBatchId = importBatchId
    };
}

public sealed class CaveRevisionConflictException : InvalidOperationException
{
    public CaveRevisionConflictException(string caveId, string? expected, string? actual)
        : base($"Cave '{caveId}' changed since it was loaded. Expected revision '{expected}', actual '{actual}'.")
    {
        CaveId = caveId;
        ExpectedRevisionId = expected;
        ActualRevisionId = actual;
    }

    public string CaveId { get; }
    public string? ExpectedRevisionId { get; }
    public string? ActualRevisionId { get; }
}
