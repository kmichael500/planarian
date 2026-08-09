using Planarian.Model.Shared.Helpers;
using Planarian.Model.Shared;
using Planarian.Modules.Import.Models;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class EntranceImportPlannerTests
{
    [Fact]
    public void PrimaryEntrancePlansFromImmutableStateWithoutDatabase()
    {
        var records = new[] { new EntranceCsvModel { CountyCode = "A01", CountyCaveNumber = "7",
            EntranceName = "Main", DecimalLatitude = 35, DecimalLongitude = -86, EntranceElevationFt = 500,
            LocationQuality = "Survey Grade", IsPrimaryEntrance = true } };
        var quality = new ImportTagLookup("quality", TagTypeKeyConstant.LocationQuality, "Survey Grade", null, true, false);
        var state = new EntranceImportPlanningState("account", [quality],
            [new("cave", "Pure Cave", "A01", 7, 1, null)],
            new Dictionary<string, int>(), new Dictionary<string, int>());

        var plan = new EntranceImportPlanner().Plan(records, state, false);

        var entrance = Assert.Single(plan.Entrances);
        Assert.True(entrance.IsPrimary);
        Assert.Equal("quality", entrance.LocationQualityTagId);
    }
}
