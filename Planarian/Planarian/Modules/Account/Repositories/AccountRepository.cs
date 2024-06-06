using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Model;
using Planarian.Shared.Base;

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
                              e.EntranceStatusTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.EntranceHydrologyTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.FieldIndicationTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.EntranceLocationQualitiesTags.Count(ee =>
                                  ee.LocationQualityTag.AccountId == RequestUser.AccountId) +
                              e.GeologyTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.CaveReportedByNameTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.CartographerNameTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.EntranceReportedByNameTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.FileTypeTags.Count(ee =>
                                  ee.ExpiresOn == null &&
                                  ee.FileTypeTag.AccountId == RequestUser.AccountId) + // temp files don't count
                              e.MapStatusTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.GeologicAgeTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.PhysiographicProvinceTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.BiologyTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.ArcheologyTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId) +
                              e.CaveOtherTags.Count(ee => ee.TagType.AccountId == RequestUser.AccountId)
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

            // Update the TagTypeId in each tag table

            #region Trip Data

            await DbContext.TripTags
                .Where(e => e.TagTypeId == tagTypeId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.LeadTags
                .Where(e => e.TagTypeId == tagTypeId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            #endregion

            await DbContext.ArcheologyTags
                .Where(e => e.TagTypeId == tagTypeId && e.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.BiologyTags
                .Where(e => e.TagTypeId == tagTypeId && e.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.CaveOtherTags
                .Where(e => e.TagTypeId == tagTypeId && e.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.EntranceHydrologyTags
                .Where(e => e.TagTypeId == tagTypeId && e.Entrance.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.EntranceStatusTags
                .Where(e => e.TagTypeId == tagTypeId && e.Entrance.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.FieldIndicationTags
                .Where(e => e.TagTypeId == tagTypeId && e.Entrance.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.Files
                .Where(e => e.FileTypeTagId == tagTypeId && e.Cave.AccountId == RequestUser.AccountId)
                .Set(ee => ee.FileTypeTagId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.GeologicAgeTags
                .Where(e => e.TagTypeId == tagTypeId && e.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.GeologyTags
                .Where(e => e.TagTypeId == tagTypeId && e.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.Entrances
                .Where(e => e.LocationQualityTagId == tagTypeId && e.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.LocationQualityTagId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.MapStatusTags
                .Where(e => e.TagTypeId == tagTypeId && e.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            #region People Tags

            // TODO: Error when merging someone who already has the same tag type for CartographerNameTag
            await DbContext.CartographerNameTags
                .Where(e => e.TagTypeId == tagTypeId && e.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.EntranceReportedByNameTags
                .Where(e => e.TagTypeId == tagTypeId && e.Entrance.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();
            await DbContext.CaveReportedByNameTags
                .Where(e => e.TagTypeId == tagTypeId && e.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            #endregion

            await DbContext.PhysiographicProvinceTags
                .Where(e => e.TagTypeId == tagTypeId && e.Cave.AccountId == RequestUser.AccountId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();
        }

        await DbContext.SaveChangesAsync();
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