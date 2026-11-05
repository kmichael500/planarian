using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Model;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Repositories;

public class FeatureSettingRepository : RepositoryBase
{
    public FeatureSettingRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext,
        requestUser)
    {
    }

    public async Task<IEnumerable<FeatureSettingVm>> GetFeatureSettings(CancellationToken cancellationToken)
    {
        var featureSettings = await GetFeatureSettingsQuery()
            .Select(e => new FeatureSettingVm
            {
                Id = e.Id,
                Key = e.Key,
                IsEnabled = e.IsEnabled,
                IsDefault = e.IsDefault
            })
            .ToListAsync(cancellationToken: cancellationToken);

        return featureSettings
            .GroupBy(e => e.Key)
            .Select(group => group.OrderByDescending(e => e.IsDefault).First())
            .ToList();
    }

    public async Task<FeatureSetting?> GetFeatureSetting(FeatureKey key, CancellationToken cancellationToken)
    {
        var featureSetting = await GetFeatureSettingsQuery()
            .Where(e => e.Key == key)
            .OrderByDescending(e => e.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        return featureSetting;
    }
    
    private IQueryable<FeatureSetting> GetFeatureSettingsQuery()
    {
        return DbContext.FeatureSettings
            .Where(e => e.AccountId == RequestUser.AccountId || e.IsDefault == true);
    }
}