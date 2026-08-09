using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;

namespace Planarian.Modules.Import.Data;

/// <summary>
/// Publishes accepted import revisions inside the caller's existing import
/// transaction. Existing Caves without history receive a baseline only when
/// the import actually changes their published state.
/// </summary>
public sealed class CaveImportRevisionRepository
{
    private const int BatchSize = 1000;
    private readonly PlanarianDbContext _db;
    private readonly AccountExecutionScope _scope;

    public CaveImportRevisionRepository(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
    }

    public async Task PublishAsync(
        IReadOnlyDictionary<string, CavePublishedSnapshotV1> before,
        IReadOnlyDictionary<string, CavePublishedSnapshotV1> after,
        IReadOnlyDictionary<string, string?> expectedCurrentRevisionIds,
        IReadOnlyDictionary<string, CaveRevisionOperation> operations,
        string importBatchId,
        CancellationToken cancellationToken = default)
    {
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
                        Id = IdGenerator.Generate(),
                        AccountId = _scope.AccountId,
                        CaveId = caveId,
                        Source = CaveRevisionSource.SystemBaseline,
                        Operation = CaveRevisionOperation.Create,
                        SnapshotSchemaVersion = 1,
                        SnapshotJson = CaveSnapshotJson.Serialize(previousSnapshot)
                    };
                    _db.CaveRevisions.Add(baseline);
                    previousRevisionId = baseline.Id;
                }

                var acceptedSnapshot = operation == CaveRevisionOperation.Delete
                    ? previousSnapshot ?? throw new InvalidOperationException($"Deleted Cave '{caveId}' has no before snapshot.")
                    : currentSnapshot ?? throw new InvalidOperationException($"Published Cave '{caveId}' has no after snapshot.");
                var revision = new CaveRevision
                {
                    Id = IdGenerator.Generate(),
                    AccountId = _scope.AccountId,
                    CaveId = caveId,
                    PreviousRevisionId = previousRevisionId,
                    Source = CaveRevisionSource.Import,
                    Operation = operation,
                    ImportBatchId = importBatchId,
                    SnapshotSchemaVersion = 1,
                    SnapshotJson = CaveSnapshotJson.Serialize(acceptedSnapshot)
                };
                _db.CaveRevisions.Add(revision);

                if (operation != CaveRevisionOperation.Delete)
                {
                    if (!liveCaves.TryGetValue(caveId, out var cave))
                        throw new ImportPlanConcurrencyException($"Cave '{caveId}' disappeared before revision publication.");
                    if (cave.CurrentRevisionId != expectedCurrent)
                        throw new CaveRevisionConflictException(caveId, expectedCurrent, cave.CurrentRevisionId);
                    cave.CurrentRevisionId = revision.Id;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }
    }
}
