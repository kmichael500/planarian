using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;

namespace Planarian.Modules.Caves.Revisions;

/// <summary>
/// Publishes a bounded set of Cave revisions inside the caller's existing transaction.
/// Existing Caves without history receive a baseline only when state actually changes.
/// </summary>
public sealed class CaveBulkRevisionRepository
{
    private const int BatchSize = 1000;
    private readonly PlanarianDbContext _db;
    private readonly AccountExecutionScope _scope;

    public CaveBulkRevisionRepository(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
    }

    public async Task PublishAsync(
        IReadOnlyDictionary<string, CavePublishedSnapshotV1> before,
        IReadOnlyDictionary<string, CavePublishedSnapshotV1> after,
        IReadOnlyDictionary<string, string?> expectedCurrentRevisionIds,
        IReadOnlyDictionary<string, CaveRevisionOperation> operations,
        CaveRevisionSource source,
        string? importBatchId = null,
        CancellationToken cancellationToken = default)
    {
        if (source is not (CaveRevisionSource.Import or CaveRevisionSource.ManagerEdit))
            throw new ArgumentException(
                "Bulk revision publication only supports Import and ManagerEdit sources.", nameof(source));
        if (source == CaveRevisionSource.Import && string.IsNullOrWhiteSpace(importBatchId))
            throw new ArgumentException("Import revision publication requires an import batch.", nameof(importBatchId));
        if (source != CaveRevisionSource.Import && importBatchId is not null)
            throw new ArgumentException("Only import revisions may identify an import batch.", nameof(importBatchId));

        var mutationIds = operations.Keys.Order(StringComparer.Ordinal).ToList();
        foreach (var chunk in mutationIds.Chunk(BatchSize))
        {
            var liveIds = chunk.Where(after.ContainsKey).ToList();
            var liveCaves = liveIds.Count == 0
                ? new Dictionary<string, Cave>(StringComparer.Ordinal)
                : await _db.Caves.IgnoreQueryFilters()
                    .Where(c => c.AccountId == _scope.AccountId && liveIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, StringComparer.Ordinal, cancellationToken);

            foreach (var caveId in chunk)
            {
                before.TryGetValue(caveId, out var previousSnapshot);
                after.TryGetValue(caveId, out var currentSnapshot);
                var operation = operations[caveId];
                var expectedCurrent = expectedCurrentRevisionIds.GetValueOrDefault(caveId);

                if (previousSnapshot is not null && currentSnapshot is not null &&
                    CaveSnapshotJson.Serialize(previousSnapshot) == CaveSnapshotJson.Serialize(currentSnapshot))
                    continue;

                string? previousRevisionId = expectedCurrent;
                if (previousSnapshot is not null && expectedCurrent is null)
                {
                    var baseline = new CaveRevision
                    {
                        Id = IdGenerator.Generate(), AccountId = _scope.AccountId, CaveId = caveId,
                        Source = CaveRevisionSource.SystemBaseline, Operation = CaveRevisionOperation.Create,
                        SnapshotSchemaVersion = 1, SnapshotJson = CaveSnapshotJson.Serialize(previousSnapshot)
                    };
                    _db.CaveRevisions.Add(baseline);
                    previousRevisionId = baseline.Id;
                }

                var acceptedSnapshot = operation == CaveRevisionOperation.Delete
                    ? previousSnapshot ?? throw new InvalidOperationException($"Deleted Cave '{caveId}' has no before snapshot.")
                    : currentSnapshot ?? throw new InvalidOperationException($"Published Cave '{caveId}' has no after snapshot.");
                var revision = new CaveRevision
                {
                    Id = IdGenerator.Generate(), AccountId = _scope.AccountId, CaveId = caveId,
                    PreviousRevisionId = previousRevisionId, Source = source, Operation = operation,
                    ImportBatchId = importBatchId, SnapshotSchemaVersion = 1,
                    SnapshotJson = CaveSnapshotJson.Serialize(acceptedSnapshot)
                };
                _db.CaveRevisions.Add(revision);

                if (operation == CaveRevisionOperation.Delete) continue;
                if (!liveCaves.TryGetValue(caveId, out var cave))
                    throw new CaveRevisionConflictException(caveId, expectedCurrent, null);
                if (cave.CurrentRevisionId != expectedCurrent)
                    throw new CaveRevisionConflictException(caveId, expectedCurrent, cave.CurrentRevisionId);
                cave.CurrentRevisionId = revision.Id;
            }

            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }
    }
}
