using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Caves.Revisions;

public sealed record CaveMutationResult(string CaveId, string? RevisionId, bool CreatedRevision);

/// <summary>
/// Captures accepted revision state separately from live Cave state so publication compares against
/// immutable history rather than assuming the current relational projection still matches it.
/// </summary>
public sealed record CaveMutationPreparation(
    string CaveId,
    CaveRevision PreviousRevision,
    CavePublishedSnapshotV1 PreviousRevisionSnapshot,
    CavePublishedSnapshotV1 LiveSnapshotBeforeMutation);

public sealed class CaveMutationRepository : AccountScopedRepositoryBase
{
    private readonly CavePublishedSnapshotRepository _snapshots;
    private readonly CaveRevisionDiffService _diff = new();

    public CaveMutationRepository(PlanarianDbContext db, RequestUser requestUser, CavePublishedSnapshotRepository snapshots) : base(db, requestUser)
    {
        _snapshots = snapshots;
    }

    /// <summary>Joins the current transaction when present; otherwise owns begin/commit/rollback.</summary>
    public Task<T> ExecuteTransactionAsync<T>(Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default) =>
        RunInTransactionAsync(work, cancellationToken);

    /// <summary>Requires transaction ownership and fails if the DbContext already has a transaction.</summary>
    public Task ExecuteOwnedTransactionAsync(Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default) =>
        RunInOwnedTransactionAsync(work, cancellationToken);

    /// <summary>Requires transaction ownership and fails if the DbContext already has a transaction.</summary>
    public Task<T> ExecuteOwnedTransactionAsync<T>(Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default) =>
        RunInOwnedTransactionAsync(work, cancellationToken);

    public Task<CaveMutationResult> PublishExistingAsync(string caveId, string? expectedRevisionId,
        CaveRevisionSource source, CaveRevisionOperation operation, Action<Cave> write,
        string? changeRequestId = null, string? importBatchId = null, CancellationToken cancellationToken = default) =>
        RunInOwnedTransactionAsync(async token =>
        {
            var preparation = await PrepareExistingAsync(caveId, expectedRevisionId, token);
            var cave = await GetOwnedCaveAsync(caveId, token);
            write(cave);
            await SaveChangesAsync(token);
            return await PublishPreparedAsync(preparation, source, operation, changeRequestId, importBatchId, token);
        }, cancellationToken);

    /// <summary>
    /// Captures the accepted revision and live snapshot for an existing Cave inside
    /// the caller-owned mutation transaction. Missing rollout history fails closed.
    /// </summary>
    public async Task<CaveMutationPreparation> PrepareExistingAsync(string caveId, string? expectedRevisionId = null,
        CancellationToken cancellationToken = default)
    {
        RequireCallerTransaction();
        var tracked = DbContext.ChangeTracker.Entries<Cave>()
            .SingleOrDefault(entry => entry.Entity.Id == caveId && entry.Entity.AccountId == Scope.AccountId);
        var cave = await GetOwnedCaveAsync(caveId, cancellationToken);
        if (tracked is not null)
        {
            // A Cave may have been tracked before County/TagType locks; refresh it before checking the expected revision.
            await tracked.ReloadAsync(cancellationToken);
            cave = tracked.Entity;
        }
        if (expectedRevisionId is not null && cave.CurrentRevisionId != expectedRevisionId)
            throw new CaveRevisionConflictException(caveId, expectedRevisionId, cave.CurrentRevisionId);

        var previous = await GetCurrentRevisionAsync(cave, cancellationToken);
        var live = await _snapshots.BuildAsync(caveId, cancellationToken);
        return new CaveMutationPreparation(caveId, previous.Revision, previous.Snapshot, live);
    }

    /// <summary>
    /// Publishes the post-mutation snapshot in the caller transaction. Semantic no-ops create no revision,
    /// and the current revision must still match the preparation.
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
        DbContext.CaveRevisions.Add(revision);
        await SaveChangesAsync(cancellationToken);
        cave.CurrentRevisionId = revision.Id;
        await SaveChangesAsync(cancellationToken);
        return new CaveMutationResult(cave.Id, revision.Id, true);
    }

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
        DbContext.CaveRevisions.Add(revision);
        await SaveChangesAsync(cancellationToken);
        cave.CurrentRevisionId = revision.Id;
        await SaveChangesAsync(cancellationToken);
        return new CaveMutationResult(cave.Id, revision.Id, true);
    }

    /// <summary>
    /// Writes the final tombstone after relational removal, using the pre-delete live snapshot and prior revision link.
    /// </summary>
    public async Task<CaveMutationResult> PublishPreparedDeleteAsync(CaveMutationPreparation preparation,
        CaveRevisionSource source, string? changeRequestId = null, string? importBatchId = null,
        CancellationToken cancellationToken = default)
    {
        RequireCallerTransaction();
        var stillExists = await DbContext.Caves.IgnoreQueryFilters()
            .AnyAsync(c => c.Id == preparation.CaveId && c.AccountId == Scope.AccountId, cancellationToken);
        if (stillExists)
            throw new InvalidOperationException("Cave must be removed before publishing its delete revision.");

        var revision = NewRevision(Scope.AccountId, preparation.CaveId, preparation.PreviousRevision,
            preparation.LiveSnapshotBeforeMutation, source, CaveRevisionOperation.Delete, changeRequestId,
            importBatchId);
        DbContext.CaveRevisions.Add(revision);
        await SaveChangesAsync(cancellationToken);
        return new CaveMutationResult(preparation.CaveId, revision.Id, true);
    }

    private async Task<Cave> GetOwnedCaveAsync(string caveId, CancellationToken cancellationToken) =>
        await DbContext.Caves.IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.Id == caveId && c.AccountId == Scope.AccountId, cancellationToken)
        ?? throw new InvalidOperationException("Cave is not owned by the current account.");

    private async Task<(CaveRevision Revision, CavePublishedSnapshotV1 Snapshot)> GetCurrentRevisionAsync(
        Cave cave, CancellationToken cancellationToken)
    {
        if (cave.CurrentRevisionId is null)
            throw new InvalidOperationException(
                $"Cave '{cave.Id}' has no current revision. Revision history rollout must complete before Cave mutations are served.");

        var revision = await DbContext.CaveRevisions
            .SingleAsync(r => r.Id == cave.CurrentRevisionId && r.AccountId == Scope.AccountId, cancellationToken);
        return (revision, CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion));
    }

    private void RequireCallerTransaction()
    {
        if (DbContext.Database.CurrentTransaction is null)
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
