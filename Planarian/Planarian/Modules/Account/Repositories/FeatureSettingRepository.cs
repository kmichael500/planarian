using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Model;
using Planarian.Shared.Base;

namespace Planarian.Modules.FeatureSettings.Repositories;

public class FeatureSettingRepository : RepositoryBase
{
    public FeatureSettingRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext,
        requestUser)
    {
    }

    public async Task<IEnumerable<FeatureSettingVm>> GetFeatureSettings(CancellationToken cancellationToken)
    {
        return await DbContext.FeatureSettings
            .Where(e => e.AccountId == RequestUser.AccountId || e.UserId == RequestUser.AccountId)
            .Select(e => new FeatureSettingVm
            {
                Id = e.Id,
                Key = e.Key,
                IsEnabled = e.IsEnabled,
            })
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<FeatureSetting?> GetFeatureSetting(FeatureKey key, CancellationToken cancellationToken)
    {
        var featureSetting = await DbContext.FeatureSettings
            .Where(e => e.AccountId == RequestUser.AccountId || e.UserId == RequestUser.Id)
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken);

        return featureSetting;
    }
}