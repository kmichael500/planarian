using System.Linq.Expressions;
using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Model;
using Planarian.Shared.Base;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Account.Repositories;

public class AccountRepository : RepositoryBase
{
    public AccountRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task DeleteAllCaves(IProgress<string> progress, CancellationToken cancellationToken)
    {
        const int batchSize = 500; // Adjust based on your needs
        var cavesCount = await AsyncExtensions.CountAsync(DbContext.Caves, e => e.AccountId == RequestUser.AccountId,
            cancellationToken);
        // Batch delete for Caves
        int deletedCount;
        var totalDeleted = 0;
        do
        {
            deletedCount = await DbContext.Caves
                .Where(e => e.AccountId == RequestUser.AccountId)
                .Where(c => DbContext.Caves
                    .Where(e => e.AccountId == RequestUser.AccountId)
                    .Take(batchSize)
                    .Select(w => w.Id)
                    .Contains(c.Id) && c.AccountId == RequestUser.AccountId)
                .DeleteAsync(cancellationToken);

            totalDeleted += deletedCount;
            progress.Report($"Deleted {totalDeleted} of {cavesCount} caves.");
        } while (deletedCount == batchSize); // Continue until fewer than batchSize rows are deleted

        // Reset counter for TagTypes
        totalDeleted = 0;

        // Batch delete for TagTypes
        var tagTypesCount =
            await AsyncExtensions.CountAsync(DbContext.TagTypes, e => e.AccountId == RequestUser.AccountId,
                cancellationToken);
        do
        {
            deletedCount = await DbContext.TagTypes
                .Where(e => e.AccountId == RequestUser.AccountId)
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

            // commit transaction
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

    public async Task MergeTagTypes(string[] tagTypeIds, string destinationTagTypeId)
    {
        foreach (var tagTypeId in tagTypeIds)
        {
            if (tagTypeId == destinationTagTypeId) continue; // skip if it's the same as destination

            #region Project Tags

            // await MergeTagsWithConflictHandling<LeadTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId);
            // await MergeTagsWithConflictHandling<TripTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId);

            #endregion

            #region Cave Tags

            await MergeTagsWithConflictHandling<ArcheologyTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId, e=>e.Cave!.AccountId);
            await MergeTagsWithConflictHandling<BiologyTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId, e=>e.Cave!.AccountId);
            await MergeTagsWithConflictHandling<CartographerNameTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId, e=>e.Cave!.AccountId);
            await MergeTagsWithConflictHandling<CaveOtherTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId, e=>e.Cave!.AccountId);
            await MergeTagsWithConflictHandling<CaveReportedByNameTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId, e=>e.Cave!.AccountId);
            await MergeTagsWithConflictHandling<File>(tagTypeId, destinationTagTypeId, e => e.FileTypeTagId, e=>e.Cave!.AccountId);
            await MergeTagsWithConflictHandling<GeologicAgeTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId, e=>e.Cave!.AccountId);
            await MergeTagsWithConflictHandling<GeologyTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId, e=>e.Cave!.AccountId);
            await MergeTagsWithConflictHandling<MapStatusTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId, e=>e.Cave!.AccountId);
            await MergeTagsWithConflictHandling<PhysiographicProvinceTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId, e=>e.Cave!.AccountId);
            
            #endregion

            #region Entrance Tags


            await MergeTagsWithConflictHandling<Entrance>(tagTypeId, destinationTagTypeId, e => e.LocationQualityTagId, e => e.Cave!.AccountId);
            await MergeTagsWithConflictHandling<EntranceHydrologyTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId, e => e.Entrance!.Cave!.AccountId);
            await MergeTagsWithConflictHandling<EntranceReportedByNameTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId, e => e.Entrance!.Cave!.AccountId);
            await MergeTagsWithConflictHandling<EntranceStatusTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId, e => e.Entrance!.Cave!.AccountId);
            await MergeTagsWithConflictHandling<FieldIndicationTag>(tagTypeId, destinationTagTypeId, e => e.TagTypeId, e => e.Entrance!.Cave!.AccountId);

            #endregion
        }

        await DbContext.SaveChangesAsync();
    }

    private async Task MergeTagsWithConflictHandling<T>(
        string sourceTagTypeId,
        string destinationTagTypeId,
        Expression<Func<T, string>> tagTypeSelector,
        Expression<Func<T, string>> accountIdSelector) where T : class
    {
        // Remove any existing tags of the destination type for the same CaveId
        var existingTags = DbContext.Set<T>()
            .Where(tagTypeSelector.Compose(s => s == destinationTagTypeId))
            .Where(accountIdSelector.Compose(a => a == RequestUser.AccountId));

        DbContext.Set<T>().RemoveRange(existingTags);
        await DbContext.SaveChangesAsync();

        var tags = DbContext.Set<T>()
            .Where(tagTypeSelector.Compose(s => s == sourceTagTypeId))
            .Where(accountIdSelector.Compose(a => a == RequestUser.AccountId));


        await tags
            .Set(tagTypeSelector, destinationTagTypeId)
            .UpdateAsync();
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
                StateIds = e.AccountStates.Select(ee => ee.StateId)
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