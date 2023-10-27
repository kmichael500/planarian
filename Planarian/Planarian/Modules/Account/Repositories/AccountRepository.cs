using LinqToDB;
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
        var cavesCount = await DbContext.Caves.CountAsync(e => e.AccountId == RequestUser.AccountId, cancellationToken);
        // Batch delete for Caves
        int deletedCount;
        var totalDeleted = 0;
        do
        {
            deletedCount = await DbContext.Caves
                .Where(e=>e.AccountId == RequestUser.AccountId)
                .Where(c => DbContext.Caves
                    .Where(e=>e.AccountId == RequestUser.AccountId)
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
            await DbContext.TagTypes.CountAsync(e => e.AccountId == RequestUser.AccountId, cancellationToken);
        do
        {
            deletedCount = await DbContext.TagTypes
                .Where(e=>e.AccountId ==  RequestUser.AccountId)
                .Where(tt => DbContext.TagTypes
                    .Where(e=>e.AccountId == RequestUser.AccountId)
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
        return await DbContext.AccountStates.Where(c => c.AccountId == RequestUser.AccountId).ToListAsync();
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
                IsUserModifiable = !string.IsNullOrWhiteSpace(e.AccountId),
                Occurrences = e.TripTags.Count +
                              e.LeadTags.Count +
                              e.EntranceStatusTags.Count +
                              e.EntranceHydrologyTags.Count +
                              e.EntranceHydrologyFrequencyTags.Count +
                              e.FieldIndicationTags.Count +
                              e.EntranceLocationQualitiesTags.Count +
                              e.GeologyTags.Count +
                              e.FileTypeTags.Count
            })
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);

        if (key.Equals(TagTypeKeyConstant.File))
            foreach (var tag in result)
                tag.IsUserModifiable = tag is { IsUserModifiable: true, Occurrences: 0 };
        return result;
    }

    public async Task<int> GetNumberOfOccurrences(string tagTypeId)
    {
        var result = await DbContext.TagTypes
            .Where(e => e.Id == tagTypeId)
            .Select(e => e.TripTags.Count +
                         e.LeadTags.Count +
                         e.EntranceStatusTags.Count +
                         e.EntranceHydrologyTags.Count +
                         e.EntranceHydrologyFrequencyTags.Count +
                         e.FieldIndicationTags.Count +
                         e.EntranceLocationQualitiesTags.Count +
                         e.GeologyTags.Count +
                         e.FileTypeTags.Count)
            .FirstOrDefaultAsync();
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
            await DbContext.TripTags
                .Where(e => e.TagTypeId == tagTypeId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.Entrances
                .Where(e => e.LocationQualityTagId == tagTypeId)
                .Set(e => e.LocationQualityTagId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.LeadTags
                .Where(e => e.TagTypeId == tagTypeId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.EntranceStatusTags
                .Where(e => e.TagTypeId == tagTypeId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.EntranceHydrologyTags
                .Where(e => e.TagTypeId == tagTypeId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.EntranceHydrologyFrequencyTags
                .Where(e => e.TagTypeId == tagTypeId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.FieldIndicationTags
                .Where(e => e.TagTypeId == tagTypeId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.GeologyTags
                .Where(e => e.TagTypeId == tagTypeId)
                .Set(e => e.TagTypeId, destinationTagTypeId)
                .UpdateAsync();

            await DbContext.Files
                .Where(e => e.FileTypeTagId == tagTypeId)
                .Set(ee => ee.FileTypeTagId, destinationTagTypeId)
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
}