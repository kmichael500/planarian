using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Planarian.Modules.Tags.Repositories;

namespace Planarian.Modules.Import.Data;

public sealed record DeferredImportBlobDelete(string? BlobKey, string? BlobContainer);
public sealed record CaveImportExecutionResult(string ImportBatchId, IReadOnlyList<DeferredImportBlobDelete> BlobDeletes);

public sealed class CaveImportExecutionRepository
{
    private const int CaveBatchSize = 1000;
    private const int LookupBatchSize = 500;
    private const int AssociationBatchSize = 10_000;

    private readonly PlanarianDbContext _db;
    private readonly AccountExecutionScope _scope;
    private readonly CavePublishedSnapshotRepository _snapshots;
    private readonly CaveBulkRevisionRepository _revisionPublisher;
    private readonly TagReferenceLockRepository _tagReferenceLocks;
    private readonly CountyReferenceLockRepository _countyReferenceLocks;

    public CaveImportExecutionRepository(PlanarianDbContext db, RequestUser requestUser,
        CavePublishedSnapshotRepository snapshots, CaveBulkRevisionRepository revisionPublisher,
        TagReferenceLockRepository tagReferenceLocks, CountyReferenceLockRepository countyReferenceLocks)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
        _snapshots = snapshots;
        _revisionPublisher = revisionPublisher;
        _tagReferenceLocks = tagReferenceLocks;
        _countyReferenceLocks = countyReferenceLocks;
    }

    public async Task<CaveImportExecutionResult> ExecuteAsync(CaveImportPlan plan, string? sourceFileName,
        CancellationToken cancellationToken = default)
    {
        if (plan.AccountId != _scope.AccountId)
            throw new InvalidOperationException("Cave import plan belongs to another account.");

        var deferredBlobDeletes = new List<DeferredImportBlobDelete>();
        string importBatchId;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockAndVerifyExistingCountyReferencesAsync(plan, cancellationToken);
            await LockAndVerifyExistingTagReferencesAsync(plan, cancellationToken);
            await LockAndVerifyExistingCavesAsync(plan, cancellationToken);
            await RejectPendingRequestDeletionsAsync(plan, cancellationToken);
            await VerifyNonTagReferenceMetadataAsync(plan, cancellationToken);

            var mutationExistingIds = plan.Caves
                .Where(c => c.Action == CaveImportAction.Update)
                .Select(c => c.Id)
                .Concat(plan.Deletions.Select(c => c.CaveId))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var before = mutationExistingIds.Count == 0
                ? new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal)
                : (await _snapshots.BuildManyAsync(mutationExistingIds, cancellationToken))
                    .ToDictionary(s => s.CaveId, StringComparer.Ordinal);

            var batch = new CaveImportBatch
            {
                Id = IdGenerator.Generate(),
                AccountId = _scope.AccountId,
                SourceFileName = sourceFileName,
                SyncExisting = plan.SyncExisting,
                Kind = CaveImportKind.CaveCsv,
                SourceRecordCount = plan.Caves.Count,
                InsertedCount = plan.Caves.Count(c => c.Action == CaveImportAction.Insert),
                UpdatedCount = plan.Caves.Count(c => c.Action == CaveImportAction.Update),
                DeletedCount = plan.Deletions.Count,
                NoChangeCount = plan.Caves.Count(c => c.Action == CaveImportAction.NoChange)
            };
            _db.CaveImportBatches.Add(batch);
            await _db.SaveChangesAsync(cancellationToken);
            importBatchId = batch.Id;
            _db.ChangeTracker.Clear();

            await PersistReferenceCreationsAsync(plan, cancellationToken);
            await UpdateCavesAsync(plan.Caves.Where(c => c.Action == CaveImportAction.Update).ToList(), cancellationToken);
            await InsertCavesAsync(plan.Caves.Where(c => c.Action == CaveImportAction.Insert).ToList(), cancellationToken);

            var changedCaves = plan.Caves.Where(c => c.Action is CaveImportAction.Insert or CaveImportAction.Update).ToList();
            await ReplaceChangedCaveTagsAsync(changedCaves, cancellationToken);
            await DeleteCavesAsync(plan.Deletions.Select(d => d.CaveId).ToList(), deferredBlobDeletes, cancellationToken);

            var liveMutationIds = changedCaves.Select(c => c.Id).ToList();
            var after = liveMutationIds.Count == 0
                ? new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal)
                : (await _snapshots.BuildManyAsync(liveMutationIds, cancellationToken))
                    .ToDictionary(s => s.CaveId, StringComparer.Ordinal);

            var expectedRevisions = mutationExistingIds.Where(plan.ExistingTargets.ContainsKey)
                .ToDictionary(id => id, id => plan.ExistingTargets[id].CurrentRevisionId, StringComparer.Ordinal);
            var operations = new Dictionary<string, CaveRevisionOperation>(StringComparer.Ordinal);
            foreach (var cave in changedCaves)
                operations[cave.Id] = cave.Action == CaveImportAction.Insert ? CaveRevisionOperation.Create : CaveRevisionOperation.Update;
            foreach (var deletion in plan.Deletions) operations[deletion.CaveId] = CaveRevisionOperation.Delete;

            await _revisionPublisher.PublishAsync(before, after, expectedRevisions, operations,
                CaveRevisionSource.Import, importBatchId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return new CaveImportExecutionResult(importBatchId, deferredBlobDeletes.Distinct().ToList());
    }

    private async Task RejectPendingRequestDeletionsAsync(CaveImportPlan plan,
        CancellationToken cancellationToken)
    {
        var deletionIds = plan.Deletions.Select(deletion => deletion.CaveId)
            .Distinct(StringComparer.Ordinal).ToList();
        if (deletionIds.Count == 0) return;
        foreach (var chunk in deletionIds.Chunk(LookupBatchSize))
        {
            var hasPending = await _db.CaveChangeRequests.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(request => request.AccountId == _scope.AccountId &&
                                     request.Status == CaveChangeRequestStatus.Pending &&
                                     chunk.Contains(request.CaveId), cancellationToken);
            if (hasPending)
                throw new ImportPlanConcurrencyException(
                    "One or more Caves selected for sync deletion have pending change requests. " +
                    "Re-plan after those requests are resolved.");
        }
    }

    private async Task LockAndVerifyExistingCavesAsync(CaveImportPlan plan, CancellationToken cancellationToken)
    {
        if (plan.ExistingTargets.Count == 0) return;
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        var transaction = _db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
                          ?? throw new InvalidOperationException("Cave import transaction is not active.");
        foreach (var chunk in plan.ExistingTargets.Keys.Order(StringComparer.Ordinal).Chunk(LookupBatchSize))
        {
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    select "Id" from "Caves"
                    where "AccountId" = @account_id and "Id" = any(@cave_ids)
                    order by "Id" collate "C" for update
                    """;
                command.Parameters.AddWithValue("account_id", _scope.AccountId);
                command.Parameters.AddWithValue("cave_ids", chunk);
                var locked = new HashSet<string>(StringComparer.Ordinal);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) locked.Add(reader.GetString(0));
                if (locked.Count != chunk.Length)
                    throw new ImportPlanConcurrencyException("One or more planned Caves no longer belong to the current account.");
            }
            var current = await _db.Caves.IgnoreQueryFilters()
                .Where(c => c.AccountId == _scope.AccountId && chunk.Contains(c.Id))
                .AsNoTracking().Select(c => new { c.Id, c.Version, c.CurrentRevisionId }).ToListAsync(cancellationToken);
            foreach (var cave in current)
            {
                var expected = plan.ExistingTargets[cave.Id];
                if (cave.Version != expected.Version || cave.CurrentRevisionId != expected.CurrentRevisionId)
                    throw new CaveRevisionConflictException(cave.Id, expected.CurrentRevisionId, cave.CurrentRevisionId);
            }
        }
    }

    private async Task LockAndVerifyExistingTagReferencesAsync(CaveImportPlan plan,
        CancellationToken cancellationToken)
    {
        var creationTagIds = plan.TagCreations.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        var existingTagIds = plan.TagNamesById.Keys.Where(id => !creationTagIds.Contains(id)).ToList();
        var lockedTags = await _tagReferenceLocks.LockForReferenceAsync(existingTagIds, cancellationToken);
        if (lockedTags.Count != existingTagIds.Distinct(StringComparer.Ordinal).Count())
            throw new ImportPlanConcurrencyException("A referenced Tag changed ownership or disappeared.");
        foreach (var tag in lockedTags)
        {
            if (plan.TagNamesById[tag.Id] != tag.Name)
                throw new ImportPlanConcurrencyException($"Tag '{tag.Id}' was renamed after import planning.");
        }
    }

    private async Task LockAndVerifyExistingCountyReferencesAsync(CaveImportPlan plan,
        CancellationToken cancellationToken)
    {
        var createdCountyIds = plan.CountyCreations.Select(county => county.Id).ToHashSet(StringComparer.Ordinal);
        var expectations = plan.Caves.Where(cave => !createdCountyIds.Contains(cave.CountyId))
            .GroupBy(cave => cave.CountyId)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var locked = await _countyReferenceLocks.LockForReferenceAsync(expectations.Keys, cancellationToken);
        if (locked.Count != expectations.Count)
            throw new ImportPlanConcurrencyException(
                "A referenced County changed ownership or disappeared after import planning.");

        foreach (var county in locked)
        {
            var expected = expectations[county.Id];
            if (county.AccountId != _scope.AccountId || county.StateId != expected.StateId ||
                county.DisplayId != expected.CountyDisplayId || county.Name != expected.CountyName)
                throw new ImportPlanConcurrencyException($"County '{county.Id}' changed after Cave import planning.");
        }
    }

    private async Task VerifyNonTagReferenceMetadataAsync(CaveImportPlan plan, CancellationToken cancellationToken)
    {
        var stateExpectations = plan.Caves.GroupBy(c => c.StateId)
            .ToDictionary(g => g.Key, g => g.First().StateAbbreviation, StringComparer.Ordinal);
        if (stateExpectations.Count > 0)
        {
            var ids = stateExpectations.Keys.ToList();
            var states = await _db.States.Where(s => ids.Contains(s.Id)).AsNoTracking()
                .Select(s => new { s.Id, s.Abbreviation }).ToListAsync(cancellationToken);
            if (states.Count != ids.Count || states.Any(s => stateExpectations[s.Id] != s.Abbreviation))
                throw new ImportPlanConcurrencyException("A referenced State changed after Cave import planning.");
        }

        if (plan.AccountStateCreations.Count > 0)
        {
            var stateIds = plan.AccountStateCreations.Select(c => c.StateId).ToList();
            if (await _db.AccountStates.AsNoTracking().AnyAsync(s => s.AccountId == _scope.AccountId && stateIds.Contains(s.StateId), cancellationToken))
                throw new ImportPlanConcurrencyException("An AccountState was created after import planning; re-plan before committing.");
        }
        if (plan.CountyCreations.Count > 0)
        {
            var displayIds = plan.CountyCreations.Select(c => c.DisplayId).ToList();
            if (await _db.Counties.AsNoTracking().AnyAsync(c => c.AccountId == _scope.AccountId && displayIds.Contains(c.DisplayId), cancellationToken))
                throw new ImportPlanConcurrencyException("A County was created after import planning; re-plan before committing.");
        }

    }

    private async Task PersistReferenceCreationsAsync(CaveImportPlan plan, CancellationToken cancellationToken)
    {
        foreach (var chunk in plan.AccountStateCreations.Chunk(LookupBatchSize))
        {
            _db.AccountStates.AddRange(chunk.Select(c => new AccountState { Id = c.Id, AccountId = c.AccountId, StateId = c.StateId }));
            await _db.SaveChangesAsync(cancellationToken); _db.ChangeTracker.Clear();
        }
        foreach (var chunk in plan.CountyCreations.Chunk(LookupBatchSize))
        {
            _db.Counties.AddRange(chunk.Select(c => new County { Id = c.Id, AccountId = c.AccountId, StateId = c.StateId, DisplayId = c.DisplayId, Name = c.Name }));
            await _db.SaveChangesAsync(cancellationToken); _db.ChangeTracker.Clear();
        }
        foreach (var chunk in plan.TagCreations.Chunk(LookupBatchSize))
        {
            _db.TagTypes.AddRange(chunk.Select(c => new TagType(c.Name, c.Key) { Id = c.Id, AccountId = _scope.AccountId, IsDefault = false }));
            await _db.SaveChangesAsync(cancellationToken); _db.ChangeTracker.Clear();
        }
    }

    private async Task UpdateCavesAsync(IReadOnlyList<PlannedCave> updates, CancellationToken cancellationToken)
    {
        foreach (var chunk in updates.Chunk(CaveBatchSize))
        {
            var ids = chunk.Select(c => c.Id).ToList();
            var persisted = await _db.Caves.IgnoreQueryFilters().Where(c => c.AccountId == _scope.AccountId && ids.Contains(c.Id)).ToListAsync(cancellationToken);
            if (persisted.Count != ids.Count) throw new ImportPlanConcurrencyException("A Cave disappeared while applying an import update.");
            var byId = chunk.ToDictionary(c => c.Id, StringComparer.Ordinal);
            foreach (var cave in persisted) ApplyScalarValues(cave, byId[cave.Id]);
            await _db.SaveChangesAsync(cancellationToken); _db.ChangeTracker.Clear();
        }
    }

    private async Task InsertCavesAsync(IReadOnlyList<PlannedCave> inserts, CancellationToken cancellationToken)
    {
        foreach (var chunk in inserts.Chunk(CaveBatchSize))
        {
            _db.Caves.AddRange(chunk.Select(planned => { var cave = new Cave { Id = planned.Id, AccountId = _scope.AccountId }; ApplyScalarValues(cave, planned); return cave; }));
            await _db.SaveChangesAsync(cancellationToken); _db.ChangeTracker.Clear();
        }
    }

    private static void ApplyScalarValues(Cave cave, PlannedCave planned)
    {
        cave.StateId = planned.StateId; cave.CountyId = planned.CountyId; cave.CountyNumber = planned.CountyNumber;
        cave.Name = planned.Name; cave.SetAlternateNamesList(planned.AlternateNames); cave.LengthFeet = planned.LengthFeet;
        cave.DepthFeet = planned.DepthFeet; cave.MaxPitDepthFeet = planned.MaxPitDepthFeet; cave.NumberOfPits = planned.NumberOfPits;
        cave.Narrative = planned.Narrative; cave.ReportedOn = planned.ReportedOn; cave.IsArchived = planned.IsArchived;
    }

    private async Task ReplaceChangedCaveTagsAsync(IReadOnlyList<PlannedCave> changedCaves, CancellationToken cancellationToken)
    {
        var updateIds = changedCaves.Where(c => c.Action == CaveImportAction.Update).Select(c => c.Id).ToList();
        foreach (var chunk in updateIds.Chunk(AssociationBatchSize))
        {
            await _db.GeologyTags.IgnoreQueryFilters().Where(t => chunk.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.GeologicAgeTags.IgnoreQueryFilters().Where(t => chunk.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.MapStatusTags.IgnoreQueryFilters().Where(t => chunk.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.PhysiographicProvinceTags.IgnoreQueryFilters().Where(t => chunk.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.ArcheologyTags.IgnoreQueryFilters().Where(t => chunk.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.BiologyTags.IgnoreQueryFilters().Where(t => chunk.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.CaveOtherTags.IgnoreQueryFilters().Where(t => chunk.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.CartographerNameTags.IgnoreQueryFilters().Where(t => chunk.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.CaveReportedByNameTags.IgnoreQueryFilters().Where(t => chunk.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
        }
        foreach (var chunk in changedCaves.SelectMany(c => c.Tags).Chunk(AssociationBatchSize))
        {
            _db.AddRange(chunk.Select(CreateAssociation));
            await _db.SaveChangesAsync(cancellationToken); _db.ChangeTracker.Clear();
        }
    }

    private static EntityBase CreateAssociation(PlannedCaveTag tag) => tag.Role switch
    {
        CaveImportTagRole.Geology => new GeologyTag { Id = tag.Id, CaveId = tag.CaveId, TagTypeId = tag.TagTypeId },
        CaveImportTagRole.GeologicAge => new GeologicAgeTag { Id = tag.Id, CaveId = tag.CaveId, TagTypeId = tag.TagTypeId },
        CaveImportTagRole.MapStatus => new MapStatusTag { Id = tag.Id, CaveId = tag.CaveId, TagTypeId = tag.TagTypeId },
        CaveImportTagRole.PhysiographicProvince => new PhysiographicProvinceTag { Id = tag.Id, CaveId = tag.CaveId, TagTypeId = tag.TagTypeId },
        CaveImportTagRole.Archeology => new ArcheologyTag { Id = tag.Id, CaveId = tag.CaveId, TagTypeId = tag.TagTypeId },
        CaveImportTagRole.Biology => new BiologyTag { Id = tag.Id, CaveId = tag.CaveId, TagTypeId = tag.TagTypeId },
        CaveImportTagRole.Other => new CaveOtherTag { Id = tag.Id, CaveId = tag.CaveId, TagTypeId = tag.TagTypeId },
        CaveImportTagRole.Cartographer => new CartographerNameTag { Id = tag.Id, CaveId = tag.CaveId, TagTypeId = tag.TagTypeId },
        CaveImportTagRole.ReportedBy => new CaveReportedByNameTag { Id = tag.Id, CaveId = tag.CaveId, TagTypeId = tag.TagTypeId },
        _ => throw new ArgumentOutOfRangeException(nameof(tag.Role))
    };

    private async Task DeleteCavesAsync(IReadOnlyList<string> caveIds, ICollection<DeferredImportBlobDelete> deferredBlobDeletes, CancellationToken cancellationToken)
    {
        foreach (var caveChunk in caveIds.Chunk(CaveBatchSize))
        {
            var scopedIds = await _db.Caves.IgnoreQueryFilters().Where(c => c.AccountId == _scope.AccountId && caveChunk.Contains(c.Id))
                .AsNoTracking().Select(c => c.Id).ToListAsync(cancellationToken);
            if (scopedIds.Count != caveChunk.Length) throw new ImportPlanConcurrencyException("A Cave disappeared while applying a sync deletion.");
            var files = await _db.Files.IgnoreQueryFilters().Where(f => f.CaveId != null && scopedIds.Contains(f.CaveId) && f.Cave != null && f.Cave.AccountId == _scope.AccountId)
                .AsNoTracking().Select(f => new { f.Id, f.BlobKey, f.BlobContainer }).ToListAsync(cancellationToken);
            foreach (var file in files) deferredBlobDeletes.Add(new DeferredImportBlobDelete(file.BlobKey, file.BlobContainer));
            var retainedObjects = await _db.RetainedCaveFileObjects.IgnoreQueryFilters()
                .Where(row => row.AccountId == _scope.AccountId && scopedIds.Contains(row.CaveId))
                .AsNoTracking().Select(row => new { row.StorageKey, row.StoragePartition }).ToListAsync(cancellationToken);
            foreach (var retained in retainedObjects)
                deferredBlobDeletes.Add(new DeferredImportBlobDelete(retained.StorageKey, retained.StoragePartition));
            await _db.RetainedCaveFileObjects.IgnoreQueryFilters()
                .Where(row => row.AccountId == _scope.AccountId && scopedIds.Contains(row.CaveId))
                .ExecuteDeleteAsync(cancellationToken);
            var entranceIds = await _db.Entrances.IgnoreQueryFilters().Where(e => scopedIds.Contains(e.CaveId) && e.Cave != null && e.Cave.AccountId == _scope.AccountId)
                .AsNoTracking().Select(e => e.Id).ToListAsync(cancellationToken);
            foreach (var entranceChunk in entranceIds.Chunk(AssociationBatchSize))
            {
                await _db.EntranceStatusTags.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await _db.EntranceHydrologyTags.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await _db.FieldIndicationTags.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await _db.EntranceReportedByNameTags.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await _db.EntranceOtherTag.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await _db.Entrances.IgnoreQueryFilters().Where(e => entranceChunk.Contains(e.Id) && e.Cave != null && e.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            }
            await _db.CaveGeoJsons.IgnoreQueryFilters().Where(g => scopedIds.Contains(g.CaveId) && g.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.GeologyTags.IgnoreQueryFilters().Where(t => scopedIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.GeologicAgeTags.IgnoreQueryFilters().Where(t => scopedIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.MapStatusTags.IgnoreQueryFilters().Where(t => scopedIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.PhysiographicProvinceTags.IgnoreQueryFilters().Where(t => scopedIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.ArcheologyTags.IgnoreQueryFilters().Where(t => scopedIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.BiologyTags.IgnoreQueryFilters().Where(t => scopedIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.CaveOtherTags.IgnoreQueryFilters().Where(t => scopedIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.CartographerNameTags.IgnoreQueryFilters().Where(t => scopedIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.CaveReportedByNameTags.IgnoreQueryFilters().Where(t => scopedIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.Favorites.IgnoreQueryFilters().Where(f => f.AccountId == _scope.AccountId && scopedIds.Contains(f.CaveId)).ExecuteDeleteAsync(cancellationToken);
            await _db.CavePermissions.IgnoreQueryFilters().Where(p => p.AccountId == _scope.AccountId && p.CaveId != null && scopedIds.Contains(p.CaveId)).ExecuteDeleteAsync(cancellationToken);
            var fileIds = files.Select(f => f.Id).ToList();
            if (fileIds.Count > 0)
            {
                await _db.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
                    .Where(sf => sf.AccountId == _scope.AccountId && fileIds.Contains(sf.FileId))
                    .ExecuteDeleteAsync(cancellationToken);
            }
            await _db.Files.IgnoreQueryFilters().Where(f => f.CaveId != null && scopedIds.Contains(f.CaveId) && f.Cave != null && f.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
            await _db.Caves.IgnoreQueryFilters().Where(c => c.AccountId == _scope.AccountId && scopedIds.Contains(c.Id)).ExecuteDeleteAsync(cancellationToken);
        }
    }
}
