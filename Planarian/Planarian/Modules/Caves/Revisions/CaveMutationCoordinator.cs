using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Revisions;

public sealed record CaveMutationResult(string CaveId, string? RevisionId, bool CreatedRevision);

public sealed class CaveMutationCoordinator
{
    private readonly PlanarianDbContext _db;
    private readonly RequestUser _requestUser;
    private readonly CavePublishedSnapshotReader _snapshots;
    private readonly CaveRevisionDiffService _diff = new();
    private readonly AccountExecutionScope _scope;

    public CaveMutationCoordinator(PlanarianDbContext db, RequestUser requestUser, CavePublishedSnapshotReader snapshots)
    { _db = db; _requestUser = requestUser; _scope = AccountExecutionScope.Require(requestUser); _snapshots = snapshots; }

    public async Task<CaveMutationResult> PublishExistingAsync(string caveId, string? expectedRevisionId,
        CaveRevisionSource source, CaveRevisionOperation operation, Action<Cave> write,
        string? changeRequestId = null, string? importBatchId = null, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var cave = await _db.Caves.IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.Id == caveId && c.AccountId == _scope.AccountId, cancellationToken)
            ?? throw new InvalidOperationException("Cave is not owned by the current account.");
        var previous = await EstablishBaselineIfNeededAsync(cave, cancellationToken);
        if (expectedRevisionId is not null && cave.CurrentRevisionId != expectedRevisionId)
            throw new CaveRevisionConflictException(caveId, expectedRevisionId, cave.CurrentRevisionId);
        write(cave);
        await _db.SaveChangesAsync(cancellationToken);
        var current = await _snapshots.BuildAsync(caveId, cancellationToken);
        if (_diff.IsSemanticEqual(previous.Snapshot, current))
        { await transaction.CommitAsync(cancellationToken); return new CaveMutationResult(caveId, cave.CurrentRevisionId, false); }
        var revision = NewRevision(cave, previous.Revision, current, source, operation, changeRequestId, importBatchId);
        _db.CaveRevisions.Add(revision);
        await _db.SaveChangesAsync(cancellationToken);
        cave.CurrentRevisionId = revision.Id;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CaveMutationResult(caveId, revision.Id, true);
    }

    public async Task<CaveMutationResult> PublishNewAsync(Cave cave, CaveRevisionSource source,
        CaveRevisionOperation operation = CaveRevisionOperation.Create, string? changeRequestId = null,
        string? importBatchId = null, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(cave.AccountId) && cave.AccountId != _scope.AccountId)
            throw new InvalidOperationException("Cave account does not match the current account.");
        cave.AccountId = _scope.AccountId;
        _db.Caves.Add(cave);
        await _db.SaveChangesAsync(cancellationToken);
        var snapshot = await _snapshots.BuildAsync(cave.Id, cancellationToken);
        var revision = NewRevision(cave, null, snapshot, source, operation, changeRequestId, importBatchId);
        _db.CaveRevisions.Add(revision);
        await _db.SaveChangesAsync(cancellationToken);
        cave.CurrentRevisionId = revision.Id;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CaveMutationResult(cave.Id, revision.Id, true);
    }

    private async Task<(CaveRevision Revision, CavePublishedSnapshotV1 Snapshot)> EstablishBaselineIfNeededAsync(Cave cave, CancellationToken cancellationToken)
    {
        if (cave.CurrentRevisionId is not null)
        {
            var existing = await _db.CaveRevisions.SingleAsync(r => r.Id == cave.CurrentRevisionId && r.AccountId == _scope.AccountId, cancellationToken);
            return (existing, CaveSnapshotJson.Deserialize(existing.SnapshotJson, existing.SnapshotSchemaVersion));
        }
        var snapshot = await _snapshots.BuildAsync(cave.Id, cancellationToken);
        var baseline = NewRevision(cave, null, snapshot, CaveRevisionSource.SystemBaseline, CaveRevisionOperation.Create, null, null);
        _db.CaveRevisions.Add(baseline);
        await _db.SaveChangesAsync(cancellationToken);
        cave.CurrentRevisionId = baseline.Id;
        await _db.SaveChangesAsync(cancellationToken);
        return (baseline, snapshot);
    }

    private static CaveRevision NewRevision(Cave cave, CaveRevision? previous, CavePublishedSnapshotV1 snapshot,
        CaveRevisionSource source, CaveRevisionOperation operation, string? changeRequestId, string? importBatchId) => new()
    {
        AccountId = cave.AccountId, CaveId = cave.Id, PreviousRevisionId = previous?.Id, Source = source, Operation = operation,
        SnapshotSchemaVersion = 1, SnapshotJson = CaveSnapshotJson.Serialize(snapshot), ChangeRequestId = changeRequestId, ImportBatchId = importBatchId
    };
}

public sealed class CaveRevisionConflictException : InvalidOperationException
{
    public CaveRevisionConflictException(string caveId, string? expected, string? actual)
        : base($"Cave '{caveId}' changed since it was loaded. Expected revision '{expected}', actual '{actual}'.")
    { CaveId = caveId; ExpectedRevisionId = expected; ActualRevisionId = actual; }
    public string CaveId { get; }
    public string? ExpectedRevisionId { get; }
    public string? ActualRevisionId { get; }
}
