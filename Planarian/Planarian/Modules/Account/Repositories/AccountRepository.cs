using LinqToDB;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Repositories;

public class AccountRepository : RepositoryBase
{
    public AccountRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task DeleteAlLCaves(IProgress<string> progress, CancellationToken cancellationToken)
    {
        const int batchSize = 500; // Adjust based on your needs
        var cavesCount = await DbContext.Caves.CountAsync(e => e.AccountId == RequestUser.AccountId, cancellationToken);
        // Batch delete for Caves
        int deletedCount;
        var totalDeleted = 0;
        do
        {
            deletedCount = await DbContext.Caves
                .Where(c => c.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .DeleteAsync(token: cancellationToken);

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
                .Where(tt => tt.AccountId == RequestUser.AccountId)
                .Take(batchSize)
                .DeleteAsync(token: cancellationToken);

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
}