using System.Linq.Expressions;
using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using LinqToDB.Reflection;
using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Modules.Account.Model;
using Planarian.Shared.Base;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Account.Repositories;

public class AccountRepository<TDbContext> : RepositoryBase<TDbContext> where TDbContext : PlanarianDbContextBase
{
    public AccountRepository(TDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task DeleteCaveWithRelatedData(IProgress<string> progress, CancellationToken cancellationToken)
    {
        const int batchSize = 500;

        int deletedCount = 0;
        int totalDeleted = 0;

        // Step 1: Delete entrance-related tags
        progress.Report("Deleting entrance-related tags...");

        do
        {
            deletedCount = await DbContext.EntranceStatusTags
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                 .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} entrance status tags.");
        } while (deletedCount == batchSize);
        
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.EntranceHydrologyTags
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} entrance hydrology tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.FieldIndicationTags
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} field indication tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.EntranceReportedByNameTags
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} entrance reported by name tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.EntranceOtherTag
                .Where(tag => tag.Entrance.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} entrance other tags.");
        } while (deletedCount == batchSize);

        // Step 2: Delete entrances
        progress.Report("Deleting entrances...");
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.Entrances
                .Where(e => e.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} entrances.");
        } while (deletedCount == batchSize);

        // Step 3: Delete cave-related tags
        progress.Report("Deleting cave-related tags...");
        deletedCount = 0;
        totalDeleted = 0;

        totalDeleted = 0;
        do
        {
            deletedCount = await DbContext.GeologyTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} geology tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {

            // the tag.account.accountUsers is only needed to force linq2sql to generate a delete statement that works with take
            deletedCount = await DbContext.Files
                .Where(tag => tag.AccountId == RequestUser.AccountId && tag.Account.AccountUsers.Any())
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} files.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.MapStatusTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} map status tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.GeologicAgeTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} geologic age tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.PhysiographicProvinceTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} physiographic province tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.BiologyTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} biology tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.ArcheologyTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} archeology tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.CartographerNameTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} cartographer name tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.CaveReportedByNameTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} cave reported by name tags.");
        } while (deletedCount == batchSize);
        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.CaveOtherTags
                .Where(tag => tag.Cave.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} cave other tags.");
        } while (deletedCount == batchSize);

        // Step 4: Delete the cave
        progress.Report("Deleting the cave...");

        deletedCount = 0;
        totalDeleted = 0;

        do
        {
            deletedCount = await DbContext.Caves
                .Where(c => c.AccountId == RequestUser.AccountId && c.Account.AccountUsers.Any())
                .Take(batchSize)
                .IgnoreQueryFilters()
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} caves.");
        } while (deletedCount == batchSize);

        progress.Report("Deleted all caves.");
    }


    public async Task DeleteAllTagTypes(IProgress<string> progress, CancellationToken cancellationToken)
    {
        const int batchSize = 500; // Adjust based on your needs
        // Batch delete for Caves
        int deletedCount;
        var totalDeleted = 0;

        
        // Batch delete for TagTypes
        var tagTypesCount =
            await AsyncExtensions.CountAsync(DbContext.TagTypes,
                e => e.AccountId == RequestUser.AccountId && e.IsDefault == false &&
                     !string.IsNullOrWhiteSpace(e.AccountId),
                cancellationToken);
        
        do
        {
            deletedCount = await DbContext.TagTypes
                .Where(e => e.AccountId == RequestUser.AccountId && e.IsDefault == false && !string.IsNullOrWhiteSpace(e.AccountId))
                .Where(tt => DbContext.TagTypes
                    .Where(e => e.AccountId == RequestUser.AccountId)
                    .Take(batchSize)
                    .Select(w => w.Id)
                    .Contains(tt.Id) && tt.AccountId == RequestUser.AccountId)
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} of {tagTypesCount} tags.");
        } while (deletedCount == batchSize); // Continue until fewer than batchSize rows are deleted
    }

    public async Task DeleteAllCounties()
    {
        await DbContext.Counties.Where(c => c.AccountId == RequestUser.AccountId).DeleteAsync();
    }
    
    // deletes all cave permissions except view all
    public async Task DeleteAllCavePermissions()
    {
        await DbContext.CavePermissions.Where(c =>
            c.AccountId == RequestUser.AccountId &&
            !(string.IsNullOrWhiteSpace(c.CaveId) && string.IsNullOrWhiteSpace(c.CountyId))).DeleteAsync();
    }

    public async Task DeleteAllAccountStates()
    {
        await DbContext.AccountStates.Where(c => c.AccountId == RequestUser.AccountId).DeleteAsync();
    }

    public async Task<IEnumerable<AccountState>> GetAllAccountStates()
    {
        return await AsyncExtensions.ToListAsync(
            DbContext.AccountStates.Where(c => c.AccountId == RequestUser.AccountId));
    }

    public async Task<IEnumerable<TagTypeTableVm>> GetTagsForTable(string key, CancellationToken cancellationToken)
    {
        var result = await AsyncExtensions.ToListAsync(DbContext.TagTypes
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
                              e.CaveOtherTags.Count(ee => ee.Cave.AccountId == RequestUser.AccountId)
            })
            .OrderBy(e => e.Name), cancellationToken);

        if (key.Equals(TagTypeKeyConstant.File) || key.Equals(TagTypeKeyConstant.LocationQuality))
            foreach (var tag in result)
                tag.IsUserModifiable = tag is { IsUserModifiable: true, Occurrences: 0 };
        return result;
    }

    public async Task<int> GetNumberOfOccurrences(string tagTypeId)
    {
        var result = await AsyncExtensions.FirstOrDefaultAsync(DbContext.TagTypes
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
                         e.CaveOtherTags.Count
            ));
        return result;
    }

    public async Task<int> DeleteTagsAsync(IEnumerable<string> tagTypeIds, CancellationToken cancellationToken)
    {
        var deletedRecords = 0;

        await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var batch in tagTypeIds.Chunk(100))
                deletedRecords += await DbContext.TagTypes
                    .Where(e => e.AccountId == RequestUser.AccountId)
                    .Where(e => batch.Contains(e.Id))
                    .DeleteAsync(cancellationToken);

            await SaveChangesAsync(cancellationToken);
            
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            // rollback transaction
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return deletedRecords;
    }

    public async Task MergeTagTypes(string[] tagTypeIds, string destinationTagTypeId,
        CancellationToken cancellationToken)
    {
        var dbTransaction = await BeginTransactionAsync(cancellationToken);

        try
        {
            var destinationTagType = await DbContext.TagTypes
                .Where(e => e.Id == destinationTagTypeId && (e.AccountId == RequestUser.AccountId || e.IsDefault))
                .FirstOrDefaultAsyncEF(cancellationToken);

            if (destinationTagType == null)
            {
                throw ApiExceptionDictionary.NotFound("Destination tag type");
            }

            var destinationTagTypeKey = destinationTagType.Key;

            var cavesParentSet = DbContext.Caves.Where(e => e.AccountId == RequestUser.AccountId);
            var entrancesParentSet = DbContext.Entrances.Where(e => e.Cave!.AccountId == RequestUser.AccountId);
            foreach (var sourceTagTypeId in tagTypeIds)
            {
                if (sourceTagTypeId == destinationTagTypeId) continue; // skip if it's the same as destination
                
                var sourceTagType = await DbContext.TagTypes
                    .Where(e => e.Id == sourceTagTypeId && (e.AccountId == RequestUser.AccountId || e.IsDefault))
                    .FirstOrDefaultAsyncEF(cancellationToken);
                
                if (sourceTagType == null)
                {
                    throw ApiExceptionDictionary.NotFound("Source tag type");
                }
                
                cancellationToken.ThrowIfCancellationRequested();

                #region Cave Tags
                
                if (TagTypeKeyConstant.Archeology.Equals(destinationTagTypeKey))
                {
                    await DeleteDuplicateTags(cavesParentSet, e => e.ArcheologyTags, tag => tag.TagTypeId,
                        sourceTagTypeId, destinationTagTypeId, cancellationToken);
                    await MergeTags<ArcheologyTag>(sourceTagTypeId, destinationTagTypeId,
                        e => e.TagTypeId,
                        e => e.Cave!.AccountId, cancellationToken);
                }
                else if (TagTypeKeyConstant.Biology.Equals(destinationTagTypeKey))
                {
                    await DeleteDuplicateTags(cavesParentSet, e => e.BiologyTags, tag => tag.TagTypeId,
                        sourceTagTypeId, destinationTagTypeId, cancellationToken);
                    await MergeTags<BiologyTag>(sourceTagTypeId, destinationTagTypeId, e => e.TagTypeId,
                        e => e.Cave!.AccountId, cancellationToken);
                }
                else if (TagTypeKeyConstant.People.Equals(destinationTagTypeKey))
                {
                    await DeleteDuplicateTags(cavesParentSet, e => e.CartographerNameTags, tag => tag.TagTypeId,
                        sourceTagTypeId, destinationTagTypeId, cancellationToken);
                    
                    await DeleteDuplicateTags(cavesParentSet, e => e.CaveReportedByNameTags, tag => tag.TagTypeId, 
                        sourceTagTypeId, destinationTagTypeId, cancellationToken);
                    
                    await DeleteDuplicateTags(entrancesParentSet, e => e.EntranceReportedByNameTags, tag => tag.TagTypeId, 
                        sourceTagTypeId, destinationTagTypeId, cancellationToken);
                    
                    await MergeTags<CartographerNameTag>(sourceTagTypeId, destinationTagTypeId,
                        e => e.TagTypeId, e => e.Cave!.AccountId, cancellationToken);
                    await MergeTags<CaveReportedByNameTag>(sourceTagTypeId, destinationTagTypeId,
                        e => e.TagTypeId, e => e.Cave!.AccountId, cancellationToken);
                    await MergeTags<EntranceReportedByNameTag>(sourceTagTypeId, destinationTagTypeId,
                        e => e.TagTypeId, e => e.Entrance!.Cave!.AccountId, cancellationToken);
                }
                else if (TagTypeKeyConstant.CaveOther.Equals(destinationTagTypeKey))
                {
                    await DeleteDuplicateTags(cavesParentSet, e => e.CaveOtherTags, tag => tag.TagTypeId,
                        sourceTagTypeId, destinationTagTypeId, cancellationToken);
                    await MergeTags<CaveOtherTag>(sourceTagTypeId, destinationTagTypeId, e => e.TagTypeId,
                        e => e.Cave!.AccountId, cancellationToken);
                }
                else if (TagTypeKeyConstant.File.Equals(destinationTagTypeKey))
                {
                    // we do not delete duplicate file tags because each file can only have one tag. it would remove the file in some cases
                    await MergeTags<File>(sourceTagTypeId, destinationTagTypeId, e => e.FileTypeTagId,
                        e => e.Cave!.AccountId, cancellationToken);
                }
                else if (TagTypeKeyConstant.GeologicAge.Equals(destinationTagTypeKey))
                {
                    await DeleteDuplicateTags(cavesParentSet, e => e.GeologicAgeTags, tag => tag.TagTypeId,
                        sourceTagTypeId, destinationTagTypeId, cancellationToken);
                    await MergeTags<GeologicAgeTag>(sourceTagTypeId, destinationTagTypeId,
                        e => e.TagTypeId,
                        e => e.Cave!.AccountId, cancellationToken);
                }
                else if (TagTypeKeyConstant.Geology.Equals(destinationTagTypeKey))
                {
                    await DeleteDuplicateTags(cavesParentSet, e => e.GeologyTags, tag => tag.TagTypeId,
                        sourceTagTypeId, destinationTagTypeId, cancellationToken);
                    await MergeTags<GeologyTag>(sourceTagTypeId, destinationTagTypeId, e => e.TagTypeId,
                        e => e.Cave!.AccountId, cancellationToken);
                }
                else if (TagTypeKeyConstant.MapStatus.Equals(destinationTagTypeKey))
                {
                    await DeleteDuplicateTags(cavesParentSet, e => e.MapStatusTags, tag => tag.TagTypeId,
                        sourceTagTypeId, destinationTagTypeId, cancellationToken);
                    await MergeTags<MapStatusTag>(sourceTagTypeId, destinationTagTypeId, e => e.TagTypeId,
                        e => e.Cave!.AccountId, cancellationToken);
                }
                else if (TagTypeKeyConstant.PhysiographicProvince.Equals(destinationTagTypeKey))
                {
                    await DeleteDuplicateTags(cavesParentSet, e => e.PhysiographicProvinceTags, tag => tag.TagTypeId,
                        sourceTagTypeId, destinationTagTypeId, cancellationToken);
                    await MergeTags<PhysiographicProvinceTag>(sourceTagTypeId, destinationTagTypeId,
                        e => e.TagTypeId, e => e.Cave!.AccountId, cancellationToken);
                }
                else if (TagTypeKeyConstant.LocationQuality.Equals(destinationTagTypeKey))
                {
                    // we do not delete duplicate location quality tags because each entrance can only have one tag. it would remove the entrance in some cases
                    await MergeTags<Entrance>(sourceTagTypeId, destinationTagTypeId,
                        e => e.LocationQualityTagId, e => e.Cave!.AccountId, cancellationToken);
                }
                else if (TagTypeKeyConstant.EntranceHydrology.Equals(destinationTagTypeKey))
                {
                    await DeleteDuplicateTags(entrancesParentSet, e => e.EntranceHydrologyTags, tag => tag.TagTypeId,
                        sourceTagTypeId, destinationTagTypeId, cancellationToken);
                    await MergeTags<EntranceHydrologyTag>(sourceTagTypeId, destinationTagTypeId,
                        e => e.TagTypeId, e => e.Entrance!.Cave!.AccountId, cancellationToken);
                }
                else if (TagTypeKeyConstant.EntranceStatus.Equals(destinationTagTypeKey))
                {
                    await DeleteDuplicateTags(entrancesParentSet, e => e.EntranceStatusTags, tag => tag.TagTypeId,
                        sourceTagTypeId, destinationTagTypeId, cancellationToken);
                    await MergeTags<EntranceStatusTag>(sourceTagTypeId, destinationTagTypeId,
                        e => e.TagTypeId, e => e.Entrance!.Cave!.AccountId, cancellationToken);
                }
                else if (TagTypeKeyConstant.FieldIndication.Equals(destinationTagTypeKey))
                {
                    await DeleteDuplicateTags(entrancesParentSet, e => e.FieldIndicationTags, tag => tag.TagTypeId,
                        sourceTagTypeId, destinationTagTypeId, cancellationToken);
                    await MergeTags<FieldIndicationTag>(sourceTagTypeId, destinationTagTypeId,
                        e => e.TagTypeId, e => e.Entrance!.Cave!.AccountId, cancellationToken);
                }

                #endregion
            }

            await DbContext.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task MergeTags<T>(string sourceTagTypeId,
        string destinationTagTypeId,
        Expression<Func<T, string>> tagTypeSelector,
        Expression<Func<T, string>> accountIdSelector,
        CancellationToken cancellationToken) where T : class
    {
        // Update the source tags to the destination type.
        var tags = DbContext.Set<T>()
            .Where(tagTypeSelector.Compose(s => s == sourceTagTypeId))
            .Where(accountIdSelector.Compose(a => a == RequestUser.AccountId));

        await tags
            .IgnoreQueryFilters()
            .Set(tagTypeSelector, destinationTagTypeId).UpdateAsync(cancellationToken);
    }

    private async Task DeleteDuplicateTags<TEntity, TTag>(IQueryable<TEntity> parentSet,
        Expression<Func<TEntity, IEnumerable<TTag>>> tagCollectionSelector,
        Expression<Func<TTag, string>> tagTypeIdSelector,
        string sourceTagTypeId,
        string destinationTagTypeId,
        CancellationToken cancellationToken)
        where TEntity : class
        where TTag : class
    {
        // Extract the collection property name.
        if (!(tagCollectionSelector.Body is MemberExpression memberExpression))
        {
            throw new ArgumentException("tagCollectionSelector must be a member access expression.",
                nameof(tagCollectionSelector));
        }

        var collectionPropertyName = memberExpression.Member.Name;

        // Extract the tag type property name.
        if (!(tagTypeIdSelector.Body is MemberExpression tagMemberExpression))
        {
            throw new ArgumentException("tagTypeIdSelector must be a member access expression.",
                nameof(tagTypeIdSelector));
        }

        var tagTypeIdPropertyName = tagMemberExpression.Member.Name;

        var query = parentSet
            .Where(e =>
                EF.Property<IEnumerable<TTag>>(e, collectionPropertyName)
                    .Any(tag => EF.Property<string>(tag, tagTypeIdPropertyName) == sourceTagTypeId)
                &&
                EF.Property<IEnumerable<TTag>>(e, collectionPropertyName)
                    .Any(tag => EF.Property<string>(tag, tagTypeIdPropertyName) == destinationTagTypeId)
            )
            .SelectMany(e => EF.Property<IEnumerable<TTag>>(e, collectionPropertyName))
            .Where(tag => EF.Property<string>(tag, tagTypeIdPropertyName) == sourceTagTypeId);

        // Execute the deletion of duplicate (source) tags.
        await query.ExecuteDeleteAsync(cancellationToken);
    }


    private void UpdateTagTypeId<T>(ICollection<T> tags, string destinationTagTypeId) where T : class
    {
        var propertyInfo = typeof(T).GetProperty("TagTypeId");
        if (propertyInfo == null) return;

        foreach (var tag in tags) propertyInfo.SetValue(tag, destinationTagTypeId);
    }

    public async Task<IEnumerable<TagTypeTableCountyVm>> GetCountiesForTable(string stateId,
        CancellationToken cancellationToken)
    {
        var result = await AsyncExtensions.ToListAsync(DbContext.Counties
            .Where(e => e.AccountId == RequestUser.AccountId && e.StateId == stateId)
            .Select(e => new TagTypeTableCountyVm
            {
                TagTypeId = e.Id,
                CountyDisplayId = e.DisplayId,
                Name = e.Name,
                IsUserModifiable = !e.Caves.Any(),
                Occurrences = e.Caves.Count()

            })
            .OrderBy(e => e.Name), cancellationToken);

        return result;
    }

    public async Task<County?> GetCounty(string? countyId, CancellationToken cancellationToken)
    {
        return await AsyncExtensions.FirstOrDefaultAsync(DbContext.Counties, e => e.Id == countyId);
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetAllStates(CancellationToken cancellationToken)
    {
        return await AsyncExtensions.ToListAsync(DbContext.States
            .Select(e => new SelectListItem<string>
            {
                Value = e.Id,
                Display = e.Abbreviation
            })
            .OrderBy(e => e.Display), cancellationToken);
    }

    public async Task<bool> IsDuplicateCountyCode(string countyDisplayId, string stateId,
        CancellationToken cancellationToken)
    {
        return await EntityFrameworkQueryableExtensions.AnyAsync(DbContext.Counties, e =>
                EF.Functions.ILike(e.DisplayId, $"{countyDisplayId}") && e.DisplayId.Length == countyDisplayId.Length &&
                e.StateId == stateId && e.AccountId == RequestUser.AccountId,
            cancellationToken);
    }

    public async Task<bool> CanDeleteCounty(string countyId)
    {
        return DbContext.Counties.Any(e =>
            e.Id == countyId && e.Caves.Count.Equals(0) && e.AccountId == RequestUser.AccountId);
    }

    public async Task MergeCounties(string[] countyIds, string destinationCountyId)
    {
        foreach (var countyId in countyIds)
        {
            if (countyId == destinationCountyId) continue;

            await DbContext.Caves
                .Where(e => e.CountyId == countyId)
                .Set(e => e.CountyId, destinationCountyId)
                .UpdateAsync();
        }

        await DbContext.SaveChangesAsync();
    }

    public async Task<MiscAccountSettingsVm?> GetMiscAccountSettingsVm(CancellationToken cancellationToken)
    {
        var account = await DbContext.Accounts
            .Where(e => e.Id == RequestUser.AccountId)
            .Select(e => new MiscAccountSettingsVm
            {
                AccountName = e.Name,
                CountyIdDelimiter = e.CountyIdDelimiter,
                StateIds = e.AccountStates.Select(ee => ee.StateId),
                DefaultViewAccessAllCaves = e.DefaultViewAccessAllCaves,
                ExportEnabled = e.ExportEnabled
            })
            .FirstOrDefaultAsyncEF(cancellationToken);

        return account;
    }

    public async Task<Planarian.Model.Database.Entities.RidgeWalker.Account?> GetAccount(
        CancellationToken cancellationToken)
    {
        return await DbContext.Accounts
            .Include(e => e.AccountStates)
            .FirstOrDefaultAsyncEF(e => e.Id == RequestUser.AccountId, cancellationToken);
    }

    public async Task<int> GetNumberOfCavesForState(string deletedStateId, CancellationToken cancellationToken)
    {
        return await DbContext.Caves.Where(e => e.StateId == deletedStateId && e.AccountId == RequestUser.AccountId)
            .CountAsyncEF(cancellationToken);
    }


    public async Task<AccountState?> GetAccountState(string accountId, string deletedStateId)
    {
        return await DbContext.AccountStates
            .Where(e => e.AccountId == accountId && e.StateId == deletedStateId)
            .FirstOrDefaultAsyncEF();
    }

    public async Task<IEnumerable<string>> GetCavesBatch(int cavesBatchSize, CancellationToken cancellationToken)
    {
        return await DbContext.Caves
            .Where(e => e.AccountId == RequestUser.AccountId)
            .Take(cavesBatchSize)
            .Select(e=>e.Id)
            .ToListAsyncEF(cancellationToken);
    }

    public async Task<int> GetCavesCount(CancellationToken cancellationToken)
    {
        return await DbContext.Caves
            .Where(e => e.AccountId == RequestUser.AccountId)
            .CountAsyncEF(cancellationToken);
    }

    public async Task<string?> GetAccountName(string accountId)
    {
        return await DbContext.Accounts
            .Where(e => e.Id == accountId)
            .Select(e => e.Name)
            .FirstOrDefaultAsyncEF();
    }

    public async Task<AccountUser?> GetAccountUser(string userId, string accountId)
    {
        return await DbContext.AccountUsers
            .Where(e => e.UserId == userId && e.AccountId == accountId)
            .FirstOrDefaultAsyncEF();
    }

    public async Task<bool> GetDefaultViewAccess()
    {
        return await DbContext.Accounts.Where(e=>e.Id == RequestUser.AccountId)
            .Select(e => e.DefaultViewAccessAllCaves)
            .FirstOrDefaultAsyncEF();
    }
}

public static class ExpressionExtensions
{
    public static Expression<Func<T, bool>> Compose<T>(this Expression<Func<T, string>> selector,
        Expression<Func<string, bool>> condition)
    {
        var parameter = selector.Parameters[0];
        var body = Expression.Invoke(condition, selector.Body);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

public class AccountRepository : AccountRepository<PlanarianDbContext>
{
    public AccountRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }
}