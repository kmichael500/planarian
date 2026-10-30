using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Caves.Services;
using Xunit;

namespace Planarian.Tests.Integration.Caves;

public sealed class CaveExportFieldContractTests
{
    [Fact]
    public void ExportFieldsParseOnlyDefinedServerFeatureKeysAndPreserveCaseInsensitiveCompatibility()
    {
        var parsed = CaveService.ParseExportFields([
            nameof(FeatureKey.EnabledFieldCaveName),
            "enabledfieldcavedepthfeet",
            "EnabledFieldCaveDistance", // Historical client-only pseudo-feature.
            "not-a-feature",
            "0" // Enum.TryParse accepts numeric strings unless explicitly rejected.
        ]);

        Assert.NotNull(parsed);
        Assert.Equal(2, parsed.Count);
        Assert.Contains(FeatureKey.EnabledFieldCaveName, parsed);
        Assert.Contains(FeatureKey.EnabledFieldCaveDepthFeet, parsed);
    }

    [Fact]
    public void NullExportFieldsKeepTheAllEnabledFieldsBehavior()
    {
        Assert.Null(CaveService.ParseExportFields(null));
    }
}
