using Planarian.Modules.Import.Models;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class CaveImportPlannerTests
{
    [Fact]
    public void NewCavePlansFromImmutableStateWithoutDatabase()
    {
        var records = new[] { new CaveCsvModel { CaveName = "Pure Cave", State = "AA", CountyCode = "A01",
            CountyName = "Alpha", CountyCaveNumber = 7, IsArchived = false } };
        var state = new CaveImportPlanningState("account", [new("state", "Alpha State", "AA")],
            new HashSet<string> { "state" }, [new("county", "state", "A01", "Alpha")], [], [],
            new HashSet<CaveImportUsedCountyNumber>());

        var plan = new CaveImportPlanner().Plan(records, state, false);

        var cave = Assert.Single(plan.Caves);
        Assert.Equal(CaveImportAction.Insert, cave.Action);
        Assert.Equal("Pure Cave", cave.Name);
        Assert.Empty(plan.CountyCreations);
    }
}
