using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using Npgsql;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;

namespace Planarian.Modules.Import.Planning;

public sealed class EntranceImportExecutor
{
    private const int CaveBatchSize = 500;
    private const int EntranceBatchSize = 1500;
    private const int AssociationBatchSize = 4000;

    private readonly PlanarianDbContext _db;
    private readonly AccountExecutionScope _scope;
    private readonly CavePublishedSnapshotReader _snapshots;
    private readonly ImportRevisionPublisher _revisionPublisher;

    public EntranceImportExecutor(PlanarianDbContext db, RequestUser requestUser,
        CavePublishedSnapshotReader snapshots, ImportRevisionPublisher revisionPublisher)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
        _snapshots = snapshots;
        _revisionPublisher = revisionPublisher;
    }

    public async Task<string> ExecuteAsync(EntranceImportPlan plan, string? sourceFileName,
        CancellationToken cancellationToken = default)
    {
        if (plan.AccountId != _scope.AccountId)
            throw new InvalidOperationException("Entrance import plan belongs to another account.");

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockAndVerifyTargetsAsync(plan, cancellationToken);
            await VerifyTagsAsync(plan, cancellationToken);

            var targetIds = plan.Targets.Keys.Order(StringComparer.Ordinal).ToList();
            var before = targetIds.Count == 0
                ? new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal)
                : (await _snapshots.BuildManyAsync(targetIds, cancellationToken))
                    .ToDictionary(s => s.CaveId, StringComparer.Ordinal);

            var batch = new CaveImportBatch
            {
                Id = IdGenerator.Generate(),
                AccountId = _scope.AccountId,
                SourceFileName = sourceFileName,
                SyncExisting = plan.SyncExisting,
                Kind = CaveImportKind.EntranceCsv,
                SourceRecordCount = plan.Entrances.Count,
                InsertedCount = plan.Entrances.Count,
                UpdatedCount = 0,
                DeletedCount = plan.SyncExisting ? plan.Targets.Values.Sum(t => t.ExistingEntranceCount) : 0,
                NoChangeCount = 0
            };
            _db.CaveImportBatches.Add(batch);
            await _db.SaveChangesAsync(cancellationToken);
            var batchId = batch.Id;
            _db.ChangeTracker.Clear();

            await PersistTagCreationsAsync(plan, cancellationToken);
            if (plan.SyncExisting) await DeleteExistingEntrancesAsync(targetIds, cancellationToken);
            await InsertEntrancesAsync(plan.Entrances, cancellationToken);
            await InsertTagsAsync(plan.Entrances.SelectMany(e => e.Tags).ToList(), cancellationToken);

            var after = targetIds.Count == 0
                ? new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal)
                : (await _snapshots.BuildManyAsync(targetIds, cancellationToken))
                    .ToDictionary(s => s.CaveId, StringComparer.Ordinal);
            var expected = plan.Targets.ToDictionary(pair => pair.Key, pair => pair.Value.CurrentRevisionId,
                StringComparer.Ordinal);
            var operations = targetIds.ToDictionary(id => id, _ => CaveRevisionOperation.Update,
                StringComparer.Ordinal);
            await _revisionPublisher.PublishAsync(before, after, expected, operations, batchId,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return batchId;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task LockAndVerifyTargetsAsync(EntranceImportPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Targets.Count == 0) return;
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        var dbTransaction = _db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
                            ?? throw new InvalidOperationException("Entrance import transaction is not active.");

        foreach (var chunk in plan.Targets.Keys.Order(StringComparer.Ordinal).Chunk(CaveBatchSize))
        {
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = dbTransaction;
                command.CommandText = """
                    select "Id" from "Caves"
                    where "AccountId" = @account_id and "Id" = any(@cave_ids)
                    order by "Id" for update
                    """;
                command.Parameters.AddWithValue("account_id", _scope.AccountId);
                command.Parameters.AddWithValue("cave_ids", chunk);
                var locked = new HashSet<string>(StringComparer.Ordinal);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) locked.Add(reader.GetString(0));
                if (locked.Count != chunk.Length)
                    throw new ImportPlanConcurrencyException("One or more Entrance target Caves no longer belong to the current account.");
            }

            var caves = await _db.Caves.IgnoreQueryFilters()
                .Where(c => c.AccountId == _scope.AccountId && chunk.Contains(c.Id))
                .AsNoTracking().Select(c => new { c.Id, c.Version, c.CurrentRevisionId })
                .ToListAsync(cancellationToken);
            foreach (var cave in caves)
            {
                var expected = plan.Targets[cave.Id];
                if (cave.Version != expected.Version || cave.CurrentRevisionId != expected.CurrentRevisionId)
                    throw new CaveRevisionConflictException(cave.Id, expected.CurrentRevisionId, cave.CurrentRevisionId);
            }

            var counts = await _db.Entrances.IgnoreQueryFilters()
                .Where(e => chunk.Contains(e.CaveId) && e.Cave != null && e.Cave.AccountId == _scope.AccountId)
                .AsNoTracking().GroupBy(e => e.CaveId)
                .Select(g => new { CaveId = g.Key, Count = g.Count(), Primary = g.Count(e => e.IsPrimary) })
                .ToListAsync(cancellationToken);
            var byCave = counts.ToDictionary(c => c.CaveId, StringComparer.Ordinal);
            foreach (var caveId in chunk)
            {
                var currentCount = byCave.GetValueOrDefault(caveId)?.Count ?? 0;
                var currentPrimary = byCave.GetValueOrDefault(caveId)?.Primary ?? 0;
                var expected = plan.Targets[caveId];
                if (currentCount != expected.ExistingEntranceCount || currentPrimary != expected.ExistingPrimaryCount)
                    throw new ImportPlanConcurrencyException($"Entrances for Cave '{caveId}' changed after import planning.");
            }
        }
    }

    private async Task VerifyTagsAsync(EntranceImportPlan plan, CancellationToken cancellationToken)
    {
        var creationIds = plan.TagCreations.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        var existingIds = plan.TagNamesById.Keys.Where(id => !creationIds.Contains(id)).ToList();
        foreach (var chunk in existingIds.Chunk(CaveBatchSize))
        {
            var tags = await _db.TagTypes
                .Where(t => chunk.Contains(t.Id) && (t.AccountId == _scope.AccountId || t.IsDefault))
                .AsNoTracking().Select(t => new { t.Id, t.Name }).ToListAsync(cancellationToken);
            if (tags.Count != chunk.Length)
                throw new ImportPlanConcurrencyException("A referenced Entrance Tag changed ownership or disappeared.");
            foreach (var tag in tags)
                if (plan.TagNamesById[tag.Id] != tag.Name)
                    throw new ImportPlanConcurrencyException($"Tag '{tag.Id}' was renamed after Entrance import planning.");
        }
    }

    private async Task PersistTagCreationsAsync(EntranceImportPlan plan, CancellationToken cancellationToken)
    {
        foreach (var chunk in plan.TagCreations.Chunk(CaveBatchSize))
        {
            _db.TagTypes.AddRange(chunk.Select(tag => new TagType(tag.Name, tag.Key)
            {
                Id = tag.Id,
                AccountId = _scope.AccountId,
                IsDefault = false
            }));
            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }
    }

    private async Task DeleteExistingEntrancesAsync(IReadOnlyList<string> caveIds,
        CancellationToken cancellationToken)
    {
        foreach (var caveChunk in caveIds.Chunk(CaveBatchSize))
        {
            var entranceIds = await _db.Entrances.IgnoreQueryFilters()
                .Where(e => caveChunk.Contains(e.CaveId) && e.Cave != null && e.Cave.AccountId == _scope.AccountId)
                .AsNoTracking().Select(e => e.Id).ToListAsync(cancellationToken);
            foreach (var entranceChunk in entranceIds.Chunk(AssociationBatchSize))
            {
                await _db.EntranceStatusTags.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await _db.EntranceHydrologyTags.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await _db.FieldIndicationTags.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await _db.EntranceReportedByNameTags.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await _db.EntranceOtherTag.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == _scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await _db.Entrances.IgnoreQueryFilters()
                    .Where(e => entranceChunk.Contains(e.Id) && e.Cave != null && e.Cave.AccountId == _scope.AccountId)
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }
    }

    private async Task InsertEntrancesAsync(IReadOnlyList<PlannedEntrance> entrances,
        CancellationToken cancellationToken)
    {
        foreach (var chunk in entrances.Chunk(EntranceBatchSize))
        {
            _db.Entrances.AddRange(chunk.Select(row => new Entrance
            {
                Id = row.Id,
                CaveId = row.CaveId,
                LocationQualityTagId = row.LocationQualityTagId,
                Name = row.Name,
                IsPrimary = row.IsPrimary,
                Description = row.Description,
                Location = new Point(new CoordinateZ(row.Longitude, row.Latitude, row.Elevation)) { SRID = 4326 },
                ReportedOn = row.ReportedOn,
                PitDepthFeet = row.PitDepthFeet
            }));
            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }
    }

    private async Task InsertTagsAsync(IReadOnlyList<PlannedEntranceTag> tags,
        CancellationToken cancellationToken)
    {
        foreach (var chunk in tags.Chunk(AssociationBatchSize))
        {
            _db.AddRange(chunk.Select(CreateAssociation));
            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }
    }

    private static EntityBase CreateAssociation(PlannedEntranceTag tag) => tag.Role switch
    {
        EntranceImportTagRole.Status => new EntranceStatusTag { Id = tag.Id, EntranceId = tag.EntranceId, TagTypeId = tag.TagTypeId },
        EntranceImportTagRole.Hydrology => new EntranceHydrologyTag { Id = tag.Id, EntranceId = tag.EntranceId, TagTypeId = tag.TagTypeId },
        EntranceImportTagRole.FieldIndication => new FieldIndicationTag { Id = tag.Id, EntranceId = tag.EntranceId, TagTypeId = tag.TagTypeId },
        EntranceImportTagRole.ReportedBy => new EntranceReportedByNameTag { Id = tag.Id, EntranceId = tag.EntranceId, TagTypeId = tag.TagTypeId },
        _ => throw new ArgumentOutOfRangeException(nameof(tag.Role))
    };
}
