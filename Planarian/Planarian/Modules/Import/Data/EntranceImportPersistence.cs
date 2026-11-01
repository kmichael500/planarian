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
using Planarian.Modules.Import.Planning;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;

using Planarian.Modules.Tags.Persistence;
namespace Planarian.Modules.Import.Data;

public sealed class EntranceImportPersistence : AccountScopedDataAccessBase
{
    private const int CaveBatchSize = 500;
    private const int EntranceBatchSize = 1500;
    private const int AssociationBatchSize = 4000;

    private readonly CavePublishedSnapshotReader _snapshotReader;
    private readonly CaveBulkRevisionPublisher _revisionPublisher;
    private readonly TagReferenceLocker _tagReferenceLocker;

    public EntranceImportPersistence(PlanarianDbContext db, RequestUser requestUser,
        CavePublishedSnapshotReader snapshotReader, CaveBulkRevisionPublisher revisionPublisher,
        TagReferenceLocker tagReferenceLocker) : base(db, requestUser)
    {
        _snapshotReader = snapshotReader;
        _revisionPublisher = revisionPublisher;
        _tagReferenceLocker = tagReferenceLocker;
    }

    public Task<string> ExecuteAsync(EntranceImportPlan plan, string? sourceFileName,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(plan, sourceFileName, sourceFileId: null, cancellationToken);

    public Task<string> ExecuteUploadedAsync(EntranceImportPlan plan, string sourceFileId,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(plan, sourceFileName: null, sourceFileId, cancellationToken);

    private Task<string> ExecuteCoreAsync(EntranceImportPlan plan, string? sourceFileName,
        string? sourceFileId, CancellationToken cancellationToken)
    {
        if (plan.AccountId != Scope.AccountId)
            throw new InvalidOperationException("Entrance import plan belongs to another account.");

        return RunInOwnedTransactionAsync(async token =>
        {
            await VerifyTagsAsync(plan, token);
            await LockAndVerifyTargetsAsync(plan, token);

            PersistedImportSource? persistedSource = null;
            if (sourceFileId is not null)
                persistedSource = await ImportSourceStore.PromoteAsync(
                    DbContext, Scope.AccountId, Scope.UserId, sourceFileId, token);

            var targetIds = plan.Targets.Keys.Order(StringComparer.Ordinal).ToList();
            var before = targetIds.Count == 0
                ? new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal)
                : (await _snapshotReader.BuildManyAsync(targetIds, token))
                    .ToDictionary(s => s.CaveId, StringComparer.Ordinal);

            var batch = new CaveImportBatch
            {
                Id = IdGenerator.Generate(),
                AccountId = Scope.AccountId,
                SourceFileId = persistedSource?.Id,
                SourceFileName = persistedSource?.FileName ?? sourceFileName,
                SyncExisting = plan.SyncExisting,
                Kind = CaveImportKind.EntranceCsv,
                SourceRecordCount = plan.Entrances.Count,
                InsertedCount = plan.Entrances.Count,
                UpdatedCount = 0,
                DeletedCount = plan.SyncExisting ? plan.Targets.Values.Sum(t => t.ExistingEntranceCount) : 0,
                NoChangeCount = 0
            };
            DbContext.CaveImportBatches.Add(batch);
            await SaveChangesAsync(token);
            var batchId = batch.Id;
            DbContext.ChangeTracker.Clear();

            await PersistTagCreationsAsync(plan, token);
            if (plan.SyncExisting) await DeleteExistingEntrancesAsync(targetIds, token);
            await InsertEntrancesAsync(plan.Entrances, token);
            await InsertTagsAsync(plan.Entrances.SelectMany(e => e.Tags).ToList(), token);

            var after = targetIds.Count == 0
                ? new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal)
                : (await _snapshotReader.BuildManyAsync(targetIds, token))
                    .ToDictionary(s => s.CaveId, StringComparer.Ordinal);
            var expected = plan.Targets.ToDictionary(pair => pair.Key, pair => pair.Value.CurrentRevisionId,
                StringComparer.Ordinal);
            var operations = targetIds.ToDictionary(id => id, _ => CaveRevisionOperation.Update,
                StringComparer.Ordinal);
            await _revisionPublisher.PublishAsync(before, after, expected, operations,
                CaveRevisionSource.Import, batchId,
                token);

            return batchId;
        }, cancellationToken);
    }

    private async Task LockAndVerifyTargetsAsync(EntranceImportPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Targets.Count == 0) return;
        var connection = (NpgsqlConnection)DbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        var dbTransaction = DbContext.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
                            ?? throw new InvalidOperationException("Entrance import transaction is not active.");

        foreach (var chunk in plan.Targets.Keys.Order(StringComparer.Ordinal).Chunk(CaveBatchSize))
        {
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = dbTransaction;
                command.CommandText = """
                    select "Id" from "Caves"
                    where "AccountId" = @account_id and "Id" = any(@cave_ids)
                    order by "Id" collate "C" for update
                    """;
                command.Parameters.AddWithValue("account_id", Scope.AccountId);
                command.Parameters.AddWithValue("cave_ids", chunk);
                var locked = new HashSet<string>(StringComparer.Ordinal);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) locked.Add(reader.GetString(0));
                if (locked.Count != chunk.Length)
                    throw new ImportPlanConcurrencyException("One or more Entrance target Caves no longer belong to the current account.");
            }

            var caves = await DbContext.Caves.IgnoreQueryFilters()
                .Where(c => c.AccountId == Scope.AccountId && chunk.Contains(c.Id))
                .AsNoTracking().Select(c => new { c.Id, c.Version, c.CurrentRevisionId })
                .ToListAsync(cancellationToken);
            foreach (var cave in caves)
            {
                var expected = plan.Targets[cave.Id];
                if (cave.Version != expected.Version || cave.CurrentRevisionId != expected.CurrentRevisionId)
                    throw new CaveRevisionConflictException(cave.Id, expected.CurrentRevisionId, cave.CurrentRevisionId);
            }

            var counts = await DbContext.Entrances.IgnoreQueryFilters()
                .Where(e => chunk.Contains(e.CaveId) && e.Cave != null && e.Cave.AccountId == Scope.AccountId)
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
        var lockedTags = await _tagReferenceLocker.LockForReferenceAsync(existingIds, cancellationToken);
        if (lockedTags.Count != existingIds.Distinct(StringComparer.Ordinal).Count())
            throw new ImportPlanConcurrencyException("A referenced Entrance Tag changed ownership or disappeared.");
        foreach (var tag in lockedTags)
        {
            if (plan.TagNamesById[tag.Id] != tag.Name)
                throw new ImportPlanConcurrencyException($"Tag '{tag.Id}' was renamed after Entrance import planning.");
        }
    }

    private async Task PersistTagCreationsAsync(EntranceImportPlan plan, CancellationToken cancellationToken)
    {
        await AddRangeBatchedAndClearAsync(plan.TagCreations.Select(tag => new TagType(tag.Name, tag.Key)
        {
            Id = tag.Id,
            AccountId = Scope.AccountId,
            IsDefault = false
        }), CaveBatchSize, cancellationToken);
    }

    private async Task DeleteExistingEntrancesAsync(IReadOnlyList<string> caveIds,
        CancellationToken cancellationToken)
    {
        foreach (var caveChunk in caveIds.Chunk(CaveBatchSize))
        {
            var entranceIds = await DbContext.Entrances.IgnoreQueryFilters()
                .Where(e => caveChunk.Contains(e.CaveId) && e.Cave != null && e.Cave.AccountId == Scope.AccountId)
                .AsNoTracking().Select(e => e.Id).ToListAsync(cancellationToken);
            foreach (var entranceChunk in entranceIds.Chunk(AssociationBatchSize))
            {
                await DbContext.EntranceStatusTags.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == Scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await DbContext.EntranceHydrologyTags.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == Scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await DbContext.FieldIndicationTags.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == Scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await DbContext.EntranceReportedByNameTags.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == Scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await DbContext.EntranceOtherTag.IgnoreQueryFilters().Where(t => entranceChunk.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null && t.Entrance.Cave.AccountId == Scope.AccountId).ExecuteDeleteAsync(cancellationToken);
                await DbContext.Entrances.IgnoreQueryFilters()
                    .Where(e => entranceChunk.Contains(e.Id) && e.Cave != null && e.Cave.AccountId == Scope.AccountId)
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }
    }

    private async Task InsertEntrancesAsync(IReadOnlyList<PlannedEntrance> entrances,
        CancellationToken cancellationToken)
    {
        await AddRangeBatchedAndClearAsync(entrances.Select(row => new Entrance
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
        }), EntranceBatchSize, cancellationToken);
    }

    private async Task InsertTagsAsync(IReadOnlyList<PlannedEntranceTag> tags,
        CancellationToken cancellationToken)
    {
        await AddRangeBatchedAndClearAsync(tags.Select(CreateAssociation), AssociationBatchSize, cancellationToken);
    }

    private static EntityBase CreateAssociation(PlannedEntranceTag tag) => tag.Role switch
    {
        EntranceImportTagRole.Status => new EntranceStatusTag { Id = tag.Id, EntranceId = tag.EntranceId, TagTypeId = tag.TagTypeId },
        EntranceImportTagRole.Hydrology => new EntranceHydrologyTag { Id = tag.Id, EntranceId = tag.EntranceId, TagTypeId = tag.TagTypeId },
        EntranceImportTagRole.FieldIndication => new FieldIndicationTag { Id = tag.Id, EntranceId = tag.EntranceId, TagTypeId = tag.TagTypeId },
        EntranceImportTagRole.ReportedBy => new EntranceReportedByNameTag { Id = tag.Id, EntranceId = tag.EntranceId, TagTypeId = tag.TagTypeId },
        EntranceImportTagRole.Other => new EntranceOtherTag { Id = tag.Id, EntranceId = tag.EntranceId, TagTypeId = tag.TagTypeId },
        _ => throw new ArgumentOutOfRangeException(nameof(tag.Role))
    };
}
