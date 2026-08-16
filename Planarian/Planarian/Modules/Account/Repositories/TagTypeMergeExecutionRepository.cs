using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Tags.Repositories;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Account.Repositories;

public sealed class TagTypeMergeExecutionRepository
{
    private const int BatchSize = 500;
    private readonly PlanarianDbContext _db;
    private readonly AccountExecutionScope _scope;
    private readonly TagReferenceLockRepository _tagLocks;
    private readonly CavePublishedSnapshotRepository _snapshots;
    private readonly CaveBulkRevisionRepository _revisions;
    private readonly RequestUser _requestUser;

    public TagTypeMergeExecutionRepository(PlanarianDbContext db, RequestUser requestUser,
        TagReferenceLockRepository tagLocks, CavePublishedSnapshotRepository snapshots,
        CaveBulkRevisionRepository revisions)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
        _tagLocks = tagLocks;
        _snapshots = snapshots;
        _revisions = revisions;
        _requestUser = requestUser;
    }

    public async Task ExecuteAsync(IEnumerable<string> sourceTagTypeIds, string destinationTagTypeId,
        CancellationToken cancellationToken = default)
    {
        var mutationTimestamp = DateTime.UtcNow;
        var actorUserId = !string.IsNullOrWhiteSpace(_requestUser.Id)
            ? _requestUser.Id
            : throw new InvalidOperationException("A current actor is required for TagType merge.");
        if (string.IsNullOrWhiteSpace(destinationTagTypeId))
            throw ApiExceptionDictionary.BadRequest("A destination tag type is required.");
        var destinationId = destinationTagTypeId.Trim();
        var sourceIds = sourceTagTypeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim())
            .Where(id => id != destinationId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
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
                : await _db.Caves.IgnoreQueryFilters().AsNoTracking()
                    .Where(cave => cave.AccountId == _scope.AccountId && affectedIds.Contains(cave.Id))
                    .ToDictionaryAsync(cave => cave.Id, cave => cave.CurrentRevisionId,
                        StringComparer.Ordinal, cancellationToken);
            if (expected.Count != affectedIds.Count)
                throw TagMergeConcurrencyConflict();
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
            throw TagMergeConcurrencyConflict();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static ApiException TagMergeConcurrencyConflict() => ApiExceptionDictionary.Conflict(
        "One or more affected Caves changed or disappeared during tag merge. Retry with current data.");

    private async Task<List<string>> DiscoverAffectedCaveIdsAsync(string key, IReadOnlyList<string> sourceIds,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        async Task AddAsync(IQueryable<string> query)
        {
            foreach (var caveId in await query.Distinct().ToListAsync(cancellationToken)) result.Add(caveId);
        }

        if (key == TagTypeKeyConstant.Archeology)
            await AddAsync(_db.ArcheologyTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
        else if (key == TagTypeKeyConstant.Biology)
            await AddAsync(_db.BiologyTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
        else if (key == TagTypeKeyConstant.GeologicAge)
            await AddAsync(_db.GeologicAgeTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
        else if (key == TagTypeKeyConstant.Geology)
            await AddAsync(_db.GeologyTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
        else if (key == TagTypeKeyConstant.MapStatus)
            await AddAsync(_db.MapStatusTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
        else if (key == TagTypeKeyConstant.PhysiographicProvince)
            await AddAsync(_db.PhysiographicProvinceTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
        else if (key == TagTypeKeyConstant.CaveOther)
        {
            await AddAsync(_db.CaveOtherTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
            await AddAsync(_db.EntranceOtherTag.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Entrance!.Cave!.AccountId == _scope.AccountId).Select(tag => tag.Entrance!.CaveId));
        }
        else if (key == TagTypeKeyConstant.People)
        {
            await AddAsync(_db.CartographerNameTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
            await AddAsync(_db.CaveReportedByNameTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Cave!.AccountId == _scope.AccountId).Select(tag => tag.CaveId));
            await AddAsync(_db.EntranceReportedByNameTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Entrance!.Cave!.AccountId == _scope.AccountId).Select(tag => tag.Entrance!.CaveId));
        }
        else if (key == TagTypeKeyConstant.EntranceHydrology)
            await AddAsync(_db.EntranceHydrologyTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Entrance!.Cave!.AccountId == _scope.AccountId).Select(tag => tag.Entrance!.CaveId));
        else if (key == TagTypeKeyConstant.EntranceStatus)
            await AddAsync(_db.EntranceStatusTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Entrance!.Cave!.AccountId == _scope.AccountId).Select(tag => tag.Entrance!.CaveId));
        else if (key == TagTypeKeyConstant.FieldIndication)
            await AddAsync(_db.FieldIndicationTags.IgnoreQueryFilters().Where(tag => sourceIds.Contains(tag.TagTypeId) &&
                tag.Entrance!.Cave!.AccountId == _scope.AccountId).Select(tag => tag.Entrance!.CaveId));
        else if (key == TagTypeKeyConstant.LocationQuality)
            await AddAsync(_db.Entrances.IgnoreQueryFilters().Where(entrance => sourceIds.Contains(entrance.LocationQualityTagId) &&
                entrance.Cave!.AccountId == _scope.AccountId).Select(entrance => entrance.CaveId));
        else if (key == TagTypeKeyConstant.File)
            await AddAsync(_db.Files.IgnoreQueryFilters().Where(file => sourceIds.Contains(file.FileTypeTagId) &&
                file.CaveId != null && file.Cave!.AccountId == _scope.AccountId).Select(file => file.CaveId!));

        return result.Order(StringComparer.Ordinal).ToList();
    }

    private async Task LockCavesAsync(IReadOnlyList<string> caveIds, CancellationToken cancellationToken)
    {
        if (caveIds.Count == 0) return;
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        var transaction = _db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
            ?? throw new InvalidOperationException("Tag merge transaction is not active.");
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
                throw TagMergeConcurrencyConflict();
        }
    }

    private async Task MergeOneAsync(string key, string sourceId, string destinationId,
        DateTime mutationTimestamp, string actorUserId,
        CancellationToken cancellationToken)
    {
        var caves = _db.Caves.IgnoreQueryFilters().Where(cave => cave.AccountId == _scope.AccountId);
        var entrances = _db.Entrances.IgnoreQueryFilters()
            .Where(entrance => entrance.Cave!.AccountId == _scope.AccountId);

        if (key == TagTypeKeyConstant.Archeology)
            await MergeCollectionAsync(caves, cave => cave.ArcheologyTags, sourceId, destinationId,
                _db.ArcheologyTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.Biology)
            await MergeCollectionAsync(caves, cave => cave.BiologyTags, sourceId, destinationId,
                _db.BiologyTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.GeologicAge)
            await MergeCollectionAsync(caves, cave => cave.GeologicAgeTags, sourceId, destinationId,
                _db.GeologicAgeTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.Geology)
            await MergeCollectionAsync(caves, cave => cave.GeologyTags, sourceId, destinationId,
                _db.GeologyTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.MapStatus)
            await MergeCollectionAsync(caves, cave => cave.MapStatusTags, sourceId, destinationId,
                _db.MapStatusTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.PhysiographicProvince)
            await MergeCollectionAsync(caves, cave => cave.PhysiographicProvinceTags, sourceId, destinationId,
                _db.PhysiographicProvinceTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.CaveOther)
        {
            await MergeCollectionAsync(caves, cave => cave.CaveOtherTags, sourceId, destinationId,
                _db.CaveOtherTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
            await MergeCollectionAsync(entrances, entrance => entrance.EntranceOtherTags, sourceId, destinationId,
                _db.EntranceOtherTag, tag => tag.TagTypeId, tag => tag.Entrance!.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        }
        else if (key == TagTypeKeyConstant.People)
        {
            await MergeCollectionAsync(caves, cave => cave.CartographerNameTags, sourceId, destinationId,
                _db.CartographerNameTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
            await MergeCollectionAsync(caves, cave => cave.CaveReportedByNameTags, sourceId, destinationId,
                _db.CaveReportedByNameTags, tag => tag.TagTypeId, tag => tag.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
            await MergeCollectionAsync(entrances, entrance => entrance.EntranceReportedByNameTags, sourceId, destinationId,
                _db.EntranceReportedByNameTags, tag => tag.TagTypeId, tag => tag.Entrance!.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        }
        else if (key == TagTypeKeyConstant.EntranceHydrology)
            await MergeCollectionAsync(entrances, entrance => entrance.EntranceHydrologyTags, sourceId, destinationId,
                _db.EntranceHydrologyTags, tag => tag.TagTypeId, tag => tag.Entrance!.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.EntranceStatus)
            await MergeCollectionAsync(entrances, entrance => entrance.EntranceStatusTags, sourceId, destinationId,
                _db.EntranceStatusTags, tag => tag.TagTypeId, tag => tag.Entrance!.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.FieldIndication)
            await MergeCollectionAsync(entrances, entrance => entrance.FieldIndicationTags, sourceId, destinationId,
                _db.FieldIndicationTags, tag => tag.TagTypeId, tag => tag.Entrance!.Cave!.AccountId,
                mutationTimestamp, actorUserId, cancellationToken);
        else if (key == TagTypeKeyConstant.LocationQuality)
            await _db.Entrances.IgnoreQueryFilters().Where(entrance => entrance.LocationQualityTagId == sourceId &&
                entrance.Cave!.AccountId == _scope.AccountId).ExecuteUpdateAsync(
                setters => setters.SetProperty(entrance => entrance.LocationQualityTagId, destinationId)
                    .SetProperty(entrance => entrance.ModifiedOn, mutationTimestamp)
                    .SetProperty(entrance => entrance.ModifiedByUserId, actorUserId), cancellationToken);
        else if (key == TagTypeKeyConstant.File)
            await _db.Files.IgnoreQueryFilters().Where(file => file.FileTypeTagId == sourceId && file.CaveId != null &&
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
