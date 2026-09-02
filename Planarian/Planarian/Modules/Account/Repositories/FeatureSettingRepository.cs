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
        // An account override wins over the global default for the same key. Some existing
        // accounts persist overrides for globally defaulted keys, so resolve precedence in
        // the query rather than returning duplicate effective settings to callers.
        var accountId = RequestUser.AccountId;
        if (string.IsNullOrWhiteSpace(accountId))
            return DbContext.FeatureSettings.Where(setting => setting.IsDefault);

        return DbContext.FeatureSettings
            .Where(setting => setting.AccountId == accountId ||
                setting.IsDefault && !DbContext.FeatureSettings.Any(accountSetting =>
                    accountSetting.AccountId == accountId && accountSetting.Key == setting.Key));
    }
}