using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Modules.PlanarianSettings.Services;

public static class AccountFeatureSettingDefaults
{
    public static bool IsEnabled(FeatureKey key) => key switch
    {
        FeatureKey.EnabledFieldCaveGeologicAgeTags => false,
        _ => true
    };
}
