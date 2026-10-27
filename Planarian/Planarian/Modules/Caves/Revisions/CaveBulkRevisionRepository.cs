using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;

namespace Planarian.Modules.Caves.Revisions;

/// <summary>
/// Publishes a bounded set of Cave revisions inside the caller's existing transaction.
/// Uses the same canonical snapshot and semantic-diff contract as interactive publication while keeping
/// the persistence path set-oriented. Existing Caves must already have rollout revision history initialized.
/// </summary>
public sealed class CaveBulkRevisionRepository
{
    private const int BatchSize = 1000;
    private readonly PlanarianDbContext _db;
    private readonly CaveRevisionDiffService _diff = new();
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
        var expectedRevisionIds = mutationIds
            .Where(after.ContainsKey)
            .Select(caveId => expectedCurrentRevisionIds.GetValueOrDefault(caveId))
            .Where(revisionId => revisionId is not null)
            .Select(revisionId => revisionId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var expectedRevisions = expectedRevisionIds.Count == 0
            ? new Dictionary<string, ExpectedRevisionSnapshot>(StringComparer.Ordinal)
            : await _db.CaveRevisions.AsNoTracking()
                .Where(revision => revision.AccountId == _scope.AccountId &&
                                   expectedRevisionIds.Contains(revision.Id))
                .Select(revision => new ExpectedRevisionSnapshot(revision.Id, revision.CaveId,
                    revision.SnapshotSchemaVersion, revision.SnapshotJson))
                .ToDictionaryAsync(revision => revision.Id, StringComparer.Ordinal, cancellationToken);

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

                if (previousSnapshot is not null && expectedCurrent is null)
                    throw new InvalidOperationException(
                        $"Cave '{caveId}' has no current revision. Revision history rollout must complete before bulk mutations are served.");

                // Match interactive publication semantics: compare the resulting state with the stored current
                // published revision rather than merely with the current live rows. This also captures any
                // reference metadata drift that predates the current publication boundary.
                var semanticPrevious = previousSnapshot;
                if (expectedCurrent is not null && currentSnapshot is not null)
                {
                    if (!liveCaves.TryGetValue(caveId, out var liveCave))
                        throw new CaveRevisionConflictException(caveId, expectedCurrent, null);
                    if (liveCave.CurrentRevisionId != expectedCurrent)
                        throw new CaveRevisionConflictException(caveId, expectedCurrent, liveCave.CurrentRevisionId);
                    if (!expectedRevisions.TryGetValue(expectedCurrent, out var expectedRevision) ||
                        expectedRevision.CaveId != caveId)
                        throw new InvalidOperationException(
                            $"Expected current revision '{expectedCurrent}' for Cave '{caveId}' was not found.");
                    semanticPrevious = CaveSnapshotJson.Deserialize(expectedRevision.SnapshotJson,
                        expectedRevision.SnapshotSchemaVersion);
                }

                if (semanticPrevious is not null && currentSnapshot is not null &&
                    _diff.IsSemanticEqual(semanticPrevious, currentSnapshot))
                    continue;

                var previousRevisionId = expectedCurrent;

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

    private sealed record ExpectedRevisionSnapshot(string Id, string CaveId, int SnapshotSchemaVersion,
        string SnapshotJson);
}
