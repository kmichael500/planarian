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
        return await GetFeatureSettingsQuery()
            .Select(e => new FeatureSettingVm
            {
                Id = e.Id,
                Key = e.Key,
                IsEnabled = e.IsEnabled,
                IsDefault = e.IsDefault,
                
            })
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<FeatureSetting?> GetFeatureSetting(FeatureKey key, CancellationToken cancellationToken)
    {
        var featureSetting = await GetFeatureSettingsQuery()
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken);

        return featureSetting;
    }
    
    private IQueryable<FeatureSetting> GetFeatureSettingsQuery()
    {
        return DbContext.FeatureSettings
            .Where(e => e.AccountId == RequestUser.AccountId || e.IsDefault == true);
    }
}