using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Account.Repositories;

public sealed record TagTypeMutationResult(string Id, string Name, string? AccountId);

public sealed class TagTypeAdministrationRepository : RepositoryBase
{
    private const int BatchSize = 500;
    private const int DeleteBatchSize = 100;
    private readonly AccountExecutionScope _scope;
    private readonly TagReferenceLockRepository _tagLocks;
    private readonly CavePublishedSnapshotRepository _snapshots;
    private readonly CaveBulkRevisionRepository _revisions;

    public TagTypeAdministrationRepository(PlanarianDbContext db, RequestUser requestUser,
        TagReferenceLockRepository tagLocks, CavePublishedSnapshotRepository snapshots,
        CaveBulkRevisionRepository revisions) : base(db, requestUser)
    {
        _scope = AccountExecutionScope.Require(requestUser);
        _tagLocks = tagLocks;
        _snapshots = snapshots;
        _revisions = revisions;
    }

    public async Task<TagTypeMutationResult> RenameAsync(string tagTypeId, string name,
        CancellationToken cancellationToken = default)
    {
        tagTypeId = tagTypeId.Trim();

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var locked = await _tagLocks.LockForDestructiveMutationAsync([tagTypeId], cancellationToken);
            if (locked.Count != 1) throw ApiExceptionDictionary.NotFound("Tag Type Id");

            var lockedTag = locked.Single();
            if (lockedTag.IsDefault)
                throw ApiExceptionDictionary.Forbidden("Cannot modify default tag types.");
            if (lockedTag.AccountId != _scope.AccountId)
                throw ApiExceptionDictionary.NotFound("Tag Type Id");

            var tag = await DbContext.TagTypes.IgnoreQueryFilters()
                .SingleOrDefaultAsync(row => row.AccountId == _scope.AccountId && row.Id == tagTypeId,
                    cancellationToken)
                ?? throw ApiExceptionDictionary.NotFound("Tag Type Id");

            if (string.Equals(tag.Name, name, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken);
                return Result(tag);
            }

            var affectedIds = await DiscoverAffectedCaveIdsAsync(tag.Key, [tag.Id], cancellationToken);
            await LockCavesAsync(affectedIds, cancellationToken);
            var expected = affectedIds.Count == 0
                ? new Dictionary<string, string?>(StringComparer.Ordinal)
                : await DbContext.Caves.IgnoreQueryFilters().AsNoTracking()
                    .Where(cave => cave.AccountId == _scope.AccountId && affectedIds.Contains(cave.Id))
                    .ToDictionaryAsync(cave => cave.Id, cave => cave.CurrentRevisionId,
                        StringComparer.Ordinal, cancellationToken);
            var before = affectedIds.Count == 0
                ? new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal)
                : (await _snapshots.BuildManyAsync(affectedIds, cancellationToken))
                    .ToDictionary(snapshot => snapshot.CaveId, StringComparer.Ordinal);

            tag.Name = name;
            await SaveChangesAsync(cancellationToken);

            if (affectedIds.Count > 0)
            {
                var after = (await _snapshots.BuildManyAsync(affectedIds, cancellationToken))
                    .ToDictionary(snapshot => snapshot.CaveId, StringComparer.Ordinal);
                var operations = affectedIds.ToDictionary(id => id, _ => CaveRevisionOperation.Update,
                    StringComparer.Ordinal);
                await _revisions.PublishAsync(before, after, expected, operations, CaveRevisionSource.ManagerEdit,
                    cancellationToken: cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result(tag);
        }
        catch (CaveRevisionConflictException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw TagAdministrationConcurrencyConflict();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task MergeAsync(IEnumerable<string> sourceTagTypeIds, string destinationTagTypeId,
        CancellationToken cancellationToken = default)
    {
        var mutationTimestamp = DateTime.UtcNow;
        var actorUserId = !string.IsNullOrWhiteSpace(RequestUser.Id)
            ? RequestUser.Id
            : throw new InvalidOperationException("A current actor is required for TagType merge.");
        if (string.IsNullOrWhiteSpace(destinationTagTypeId))
            throw ApiExceptionDictionary.BadRequest("A destination tag type is required.");
        var destinationId = destinationTagTypeId.Trim();
        var sourceIds = sourceTagTypeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim())
            .Where(id => id != destinationId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var requestedIds = sourceIds.Append(destinationId).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToList();
            var lockedTags = await _tagLocks.LockForDestructiveMutationAsync(requestedIds, cancellationToken);
            if (lockedTags.Count != requestedIds.Count)
                throw ApiExceptionDictionary.BadRequest(
                    "One or more source or destination tag types do not exist or are not available to this account.");
            var destination = lockedTags.Single(tag => tag.Id == destinationId);
            if (lockedTags.Any(tag => tag.Key != destination.Key))
                throw ApiExceptionDictionary.BadRequest(
                    "Source and destination tag types must use the same tag key.");
            if (!TagTypeKeyConstant.IsValidAccountTagKey(destination.Key))
                throw ApiExceptionDictionary.BadRequest(
                    "Tag merge only supports account Cave tag families.");
            if (sourceIds.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var affectedIds = await DiscoverAffectedCaveIdsAsync(destination.Key, sourceIds, cancellationToken);
            await LockCavesAsync(affectedIds, cancellationToken);
            var expected = affectedIds.Count == 0
                ? new Dictionary<string, string?>(StringComparer.Ordinal)
                : await DbContext.Caves.IgnoreQueryFilters().AsNoTracking()
                    .Where(cave => cave.AccountId == _scope.AccountId && affectedIds.Contains(cave.Id))
                    .ToDictionaryAsync(cave => cave.Id, cave => cave.CurrentRevisionId,
                        StringComparer.Ordinal, cancellationToken);
            if (expected.Count != affectedIds.Count)
                throw TagAdministrationConcurrencyConflict();
            var before = affectedIds.Count == 0
                ? new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal)
                : (await _snapshots.BuildManyAsync(affectedIds, cancellationToken))
                    .ToDictionary(snapshot => snapshot.CaveId, StringComparer.Ordinal);

            foreach (var sourceId in sourceIds)
                await MergeOneAsync(destination.Key, sourceId, destinationId, mutationTimestamp, actorUserId,
                    cancellationToken);

            var after = affectedIds.Count == 0
                ? new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal)
                : (await _snapshots.BuildManyAsync(affectedIds, cancellationToken))
                    .ToDictionary(snapshot => snapshot.CaveId, StringComparer.Ordinal);
            var operations = affectedIds.ToDictionary(id => id, _ => CaveRevisionOperation.Update,
                StringComparer.Ordinal);
            await _revisions.PublishAsync(before, after, expected, operations,
                CaveRevisionSource.ManagerEdit, cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (CaveRevisionConflictException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw TagAdministrationConcurrencyConflict();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IEnumerable<TagTypeTableVm>> GetTagsForTable(string key, CancellationToken cancellationToken)
    {
        var result = await DbContext.TagTypes
            .Where(e => e.Key == key)
            .Where(e => e.AccountId == RequestUser.AccountId || e.IsDefault)
            .Select(e => new TagTypeTableVm
            {
                TagTypeId = e.Id,
                Name = e.Name,
                IsUserModifiable = !string.IsNullOrWhiteSpace(e.AccountId) || !e.IsDefault,
                Occurrences = e.TripTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.LeadTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.EntranceStatusTags.Count(ee => ee.Entrance.Cave.AccountId == RequestUser.AccountId) +
                              e.EntranceHydrologyTags.Count(ee => ee.Entrance.Cave.AccountId == RequestUser.AccountId) +
                              e.FieldIndicationTags.Count(ee => ee.Entrance.Cave.AccountId == RequestUser.AccountId) +
                              e.EntranceLocationQualitiesTags.Count(ee =>
                                  ee.Cave.AccountId == RequestUser.AccountId) +
                              e.GeologyTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.CaveReportedByNameTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.CartographerNameTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.EntranceReportedByNameTags.Count(ee => ee.Entrance.Cave.AccountId == RequestUser.AccountId) +
                              e.FileTypeTags.Count(ee =>
                                  ee.ExpiresOn == null && ee.Cave.AccountId == RequestUser.AccountId) + // temp files don't count
                              e.MapStatusTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.GeologicAgeTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.PhysiographicProvinceTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.BiologyTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.ArcheologyTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.CaveOtherTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId) +
                              e.EntranceOtherTags.Count(ee => ee.Entrance.Cave.AccountId == RequestUser.AccountId)
            })
            .OrderBy(e => e.Name).ToListAsync(cancellationToken);

        if (key.Equals(TagTypeKeyConstant.File) || key.Equals(TagTypeKeyConstant.LocationQuality))
            foreach (var tag in result)
                tag.IsUserModifiable = tag is { IsUserModifiable: true, Occurrences: 0 };
        return result;
    }

    public async Task<int> GetNumberOfOccurrences(string tagTypeId, CancellationToken cancellationToken = default)
    {
        var result = await DbContext.TagTypes
            .Where(e => e.Id == tagTypeId)
            .Select(e => e.TripTags.Count +
                         e.LeadTags.Count +
                         e.EntranceStatusTags.Count +
                         e.EntranceHydrologyTags.Count +
                         e.FieldIndicationTags.Count +
                         e.EntranceLocationQualitiesTags.Count +
                         e.GeologyTags.Count +
                         e.CaveReportedByNameTags.Count +
                         e.CartographerNameTags.Count +
                         e.EntranceReportedByNameTags.Count +
                         e.FileTypeTags.Count +
                         e.MapStatusTags.Count +
                         e.GeologicAgeTags.Count +
                         e.PhysiographicProvinceTags.Count +
                         e.BiologyTags.Count +
                         e.ArcheologyTags.Count +
                         e.CaveOtherTags.Count +
                         e.EntranceOtherTags.Count
            ).FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    public async Task<int> DeleteAsync(IEnumerable<string> tagTypeIds,
        CancellationToken cancellationToken = default)
    {
        var accountId = RequestUser.AccountId
            ?? throw new InvalidOperationException("A current account is required for TagType deletion.");
        var ids = tagTypeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return 0;

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var chunk in ids.Chunk(DeleteBatchSize))
                await DbContext.TagTypes.FromSqlInterpolated($"""
                        select * from "TagTypes"
                        where "AccountId" = {accountId} and "Id" = any({chunk})
                        order by "Id" collate "C" for update
                        """)
                    .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);

            var deleted = 0;
            try
            {
                foreach (var chunk in ids.Chunk(DeleteBatchSize))
                    deleted += await DbContext.TagTypes.IgnoreQueryFilters()
                        .Where(tag => tag.AccountId == accountId && chunk.Contains(tag.Id))
                        .ExecuteDeleteAsync(cancellationToken);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                throw ApiExceptionDictionary.BadRequest("Cannot delete tag type because it is in use.");
            }

            await transaction.CommitAsync(cancellationToken);
            return deleted;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static ApiException TagAdministrationConcurrencyConflict() => ApiExceptionDictionary.Conflict(
        "One or more affected Caves changed or disappeared during tag administration. Retry with current data.");

    private static TagTypeMutationResult Result(TagType tag) =>
        new(tag.Id, tag.Name, tag.AccountId);

    private async Task<List<string>> DiscoverAffectedCaveIdsAsync(string key, IReadOnlyList<string> sourceIds,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        async Task AddAsync(IQueryable<string> query)
        {
            foreach (var caveId in await query.Distinct().ToListAsync(cancellationToken)) result.Add(caveId);
        }

        if (key == TagTypeKeyConstant.Archeology)
            await AddAsync(DbContext.ArcheologyTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
        else if (key == TagTypeKeyConstant.Biology)
            await AddAsync(DbContext.BiologyTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
        else if (key == TagTypeKeyConstant.GeologicAge)
            await AddAsync(DbContext.GeologicAgeTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
        else if (key == TagTypeKeyConstant.Geology)
            await AddAsync(DbContext.GeologyTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
        else if (key == TagTypeKeyConstant.MapStatus)
            await AddAsync(DbContext.MapStatusTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
        else if (key == TagTypeKeyConstant.PhysiographicProvince)
            await AddAsync(DbContext.PhysiographicProvinceTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
        else if (key == TagTypeKeyConstant.CaveOther)
        {
            await AddAsync(DbContext.CaveOtherTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
            await AddAsync(DbContext.EntranceOtherTag.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Entrance!.Cave!.AccountId == _scope.AccountId).Select(tag => tag.Entrance!.CaveId));
        }
        else if (key == TagTypeKeyConstant.People)
        {
            await AddAsync(DbContext.CartographerNameTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
            await AddAsync(DbContext.CaveReportedByNameTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
            await AddAsync(DbContext.EntranceReportedByNameTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Entrance!.Cave!.AccountId == _scope.AccountId).Select(tag => tag.Entrance!.CaveId));
        }
        else if (key == TagTypeKeyConstant.EntranceHydrology)
            await AddAsync(DbContext.EntranceHydrologyTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Entrance!.Cave!.AccountId == _scope.AccountId).Select(tag => tag.Entrance!.CaveId));
        else if (key == TagTypeKeyConstant.EntranceStatus)
            await AddAsync(DbContext.EntranceStatusTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Entrance!.Cave!.AccountId == _scope.AccountId).Select(tag => tag.Entrance!.CaveId));
        else if (key == TagTypeKeyConstant.FieldIndication)
            await AddAsync(DbContext.FieldIndicationTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Entrance!.Cave!.AccountId == _scope.AccountId).Select(tag => tag.Entrance!.CaveId));
        else if (key == TagTypeKeyConstant.LocationQuality)
            await AddAsync(DbContext.Entrances.IgnoreQueryFilters().Where(entrance => sourceIds.Contains(entrance.LocationQualityTagId) &&
                entrance.Cave!.AccountId == _scope.AccountId).Select(entrance => entrance.CaveId));
        else if (key == TagTypeKeyConstant.File)
            await AddAsync(DbContext.Files.IgnoreQueryFilters().Where(file => sourceIds.Contains(file.FileTypeTagId) &&
                file.CaveId != null && file.Cave!.AccountId == _scope.AccountId).Select(file => file.CaveId!));

        return result.Order(StringComparer.Ordinal).ToList();
    }

    private async Task LockCavesAsync(IReadOnlyList<string> caveIds, CancellationToken cancellationToken)
    {
        if (caveIds.Count == 0) return;
        var connection = (NpgsqlConnection)DbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        var transaction = DbContext.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
            ?? throw new InvalidOperationException("Tag administration transaction is not active.");
        foreach (var chunk in caveIds.Order(StringComparer.Ordinal).Chunk(BatchSize))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                select "Id" from "Caves"
                where "AccountId" = @account_id and "Id" = any(@cave_ids)
                order by "Id" collate "C" for update
                """;
            command.Parameters.AddWithValue("account_id", _scope.AccountId);
            command.Parameters.AddWithValue("cave_ids", chunk);
            var count = 0;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) count++;
            if (count != chunk.Length)
                throw TagAdministrationConcurrencyConflict();
        }
    }

    private async Task MergeOneAsync(string key, string sourceId, string destinationId,
        DateTime mutationTimestamp, string actorUserId,
        CancellationToken cancellationToken)
    {
        var caves = DbContext.Caves.IgnoreQueryFilters().Where(cave => cave.AccountId == _scope.AccountId);
        var entrances = DbContext.Entrances.IgnoreQueryFilters()
            .Where(entrance => entrance.Cave!.AccountId == _scope.AccountId);

        if (key == TagTypeKeyConstant.Archeology)
            await MergeCollectionAsync(caves, cave => cave.ArcheologyTags, sourceId, destinationId,
                DbContext.ArcheologyTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.Biology)
            await MergeCollectionAsync(caves, cave => cave.BiologyTags, sourceId, destinationId,
                DbContext.BiologyTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.GeologicAge)
            await MergeCollectionAsync(caves, cave => cave.GeologicAgeTags, sourceId, destinationId,
                DbContext.GeologicAgeTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.Geology)
            await MergeCollectionAsync(caves, cave => cave.GeologyTags, sourceId, destinationId,
                DbContext.GeologyTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.MapStatus)
            await MergeCollectionAsync(caves, cave => cave.MapStatusTags, sourceId, destinationId,
                DbContext.MapStatusTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.PhysiographicProvince)
            await MergeCollectionAsync(caves, cave => cave.PhysiographicProvinceTags, sourceId, destinationId,
                DbContext.PhysiographicProvinceTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.CaveOther)
        {
            await MergeCollectionAsync(caves, cave => cave.CaveOtherTags, sourceId, destinationId,
                DbContext.CaveOtherTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
            await MergeCollectionAsync(entrances, entrance => entrance.EntranceOtherTags, sourceId, destinationId,
                DbContext.EntranceOtherTag, tag => tag.TagTypeId, tag => tag.Entrance!.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        }
        else if (key == TagTypeKeyConstant.People)
        {
            await MergeCollectionAsync(caves, cave => cave.CartographerNameTags, sourceId, destinationId,
                DbContext.CartographerNameTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
            await MergeCollectionAsync(caves, cave => cave.CaveReportedByNameTags, sourceId, destinationId,
                DbContext.CaveReportedByNameTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
            await MergeCollectionAsync(entrances, entrance => entrance.EntranceReportedByNameTags, sourceId, destinationId,
                DbContext.EntranceReportedByNameTags, tag => tag.TagTypeId, tag => tag.Entrance!.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        }
        else if (key == TagTypeKeyConstant.EntranceHydrology)
            await MergeCollectionAsync(entrances, entrance => entrance.EntranceHydrologyTags, sourceId, destinationId,
                DbContext.EntranceHydrologyTags, tag => tag.TagTypeId, tag => tag.Entrance!.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.EntranceStatus)
            await MergeCollectionAsync(entrances, entrance => entrance.EntranceStatusTags, sourceId, destinationId,
                DbContext.EntranceStatusTags, tag => tag.TagTypeId, tag => tag.Entrance!.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.FieldIndication)
            await MergeCollectionAsync(entrances, entrance => entrance.FieldIndicationTags, sourceId, destinationId,
                DbContext.FieldIndicationTags, tag => tag.TagTypeId, tag => tag.Entrance!.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.LocationQuality)
            await DbContext.Entrances.IgnoreQueryFilters().Where(entrance => entrance.LocationQualityTagId == sourceId &&
                entrance.Cave!.AccountId == _scope.AccountId).ExecuteUpdateAsync(
                setters => setters.SetProperty(entrance => entrance.LocationQualityTagId, destinationId)
                    .SetProperty(entrance => entrance.ModifiedOn, mutationTimestamp)
                    .SetProperty(entrance => entrance.ModifiedByUserId, actorUserId), cancellationToken);
        else if (key == TagTypeKeyConstant.File)
            await DbContext.Files.IgnoreQueryFilters().Where(file => file.FileTypeTagId == sourceId && file.CaveId != null &&
                file.Cave!.AccountId == _scope.AccountId).ExecuteUpdateAsync(
                setters => setters.SetProperty(file => file.FileTypeTagId, destinationId)
                    .SetProperty(file => file.ModifiedOn, mutationTimestamp)
                    .SetProperty(file => file.ModifiedByUserId, actorUserId), cancellationToken);
    }

    private async Task MergeCollectionAsync<TParent, TTag>(IQueryable<TParent> parents,
        Expression<Func<TParent, IEnumerable<TTag>>> collectionSelector, string sourceId, string destinationId,
        IQueryable<TTag> tags, Expression<Func<TTag, string>> tagIdSelector,
        Expression<Func<TTag, string>> accountIdSelector, DateTime mutationTimestamp, string actorUserId,
        CancellationToken cancellationToken)
        where TParent : class where TTag : EntityBase
    {
        await DeleteDuplicateTagsAsync(parents, collectionSelector, tagIdSelector, sourceId, destinationId,
            cancellationToken);
        var propertyName = tagIdSelector.Body is MemberExpression member
            ? member.Member.Name
            : throw new ArgumentException("Tag merge selector must be a member access expression.");
        await tags.IgnoreQueryFilters().Where(tagIdSelector.Compose(value => value == sourceId))
            .Where(accountIdSelector.Compose(accountId => accountId == _scope.AccountId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(tag => EF.Property<string>(tag, propertyName), destinationId)
                .SetProperty(tag => tag.ModifiedOn, mutationTimestamp)
                .SetProperty(tag => tag.ModifiedByUserId, actorUserId), cancellationToken);
    }

    private static async Task DeleteDuplicateTagsAsync<TParent, TTag>(IQueryable<TParent> parents,
        Expression<Func<TParent, IEnumerable<TTag>>> collectionSelector,
        Expression<Func<TTag, string>> tagTypeIdSelector, string sourceId, string destinationId,
        CancellationToken cancellationToken) where TParent : class where TTag : class
    {
        if (collectionSelector.Body is not MemberExpression collectionMember ||
            tagTypeIdSelector.Body is not MemberExpression tagMember)
            throw new ArgumentException("Tag merge selectors must be member access expressions.");
        var query = parents.Where(parent => EF.Property<IEnumerable<TTag>>(parent, collectionMember.Member.Name)
                .Any(tag => EF.Property<string>(tag, tagMember.Member.Name) == sourceId) &&
            EF.Property<IEnumerable<TTag>>(parent, collectionMember.Member.Name)
                .Any(tag => EF.Property<string>(tag, tagMember.Member.Name) == destinationId))
            .SelectMany(parent => EF.Property<IEnumerable<TTag>>(parent, collectionMember.Member.Name))
            .Where(tag => EF.Property<string>(tag, tagMember.Member.Name) == sourceId);
        await query.ExecuteDeleteAsync(cancellationToken);
    }
}
