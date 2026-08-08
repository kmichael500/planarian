using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class ImportPreviewCommitEquivalenceTests(PostgresIntegrationFixture fixture) : IClassFixture<PostgresIntegrationFixture>
{
    [Fact]
    public async Task CavePreviewMatchesPersistedSemanticResultFromSamePlan()
    {
        await using var database=await fixture.CreateDatabaseAsync(nameof(CavePreviewMatchesPersistedSemanticResultFromSamePlan));
        var tenant=await IntegrationTestData.SeedTenantAsync(database,'a');
        CaveImportPlan plan; Planarian.Modules.Account.Import.Models.CaveDryRunRecord preview;
        await using(var db=database.CreateDbContext("a",tenant.AccountId))
        {
            var planner=new CaveImportPlanner(db,db.RequestUser);
            await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.CaveHeader+"\nCommitted Cave,Committed County,COM,77,AA,Alt,Needs Mapping,Mapper,300,55,22,3,Limestone,Mississippian,Plateau,None,Bats,2026-08-01,Reporter,false,Interesting,Committed narrative\n");
            plan=await planner.PlanAsync(csv,false); preview=Assert.Single(plan.CreatePreview(true));
            var snapshots=new CavePublishedSnapshotReader(db,db.RequestUser); var publisher=new ImportRevisionPublisher(db,db.RequestUser);
            await new CaveImportExecutor(db,db.RequestUser,snapshots,publisher).ExecuteAsync(plan,"caves.csv");
        }
        var planned=Assert.Single(plan.Caves); await using var verify=database.CreateDbContext("a",tenant.AccountId);
        var cave=await verify.Caves.IgnoreQueryFilters().SingleAsync(c=>c.Id==planned.Id);
        Assert.Equal("insert",preview.Action); Assert.Equal(preview.CaveName,cave.Name); Assert.Equal(preview.CountyCaveNumber,cave.CountyNumber);
        Assert.Equal(preview.CaveLengthFeet,cave.LengthFeet); Assert.Equal(preview.CaveDepthFeet,cave.DepthFeet); Assert.Equal(preview.Narrative,cave.Narrative); Assert.NotNull(cave.CurrentRevisionId);
        Assert.True(await verify.CaveImportBatches.AnyAsync(b=>b.SourceFileName=="caves.csv"));
        Assert.True(await verify.CaveRevisions.AnyAsync(r=>r.CaveId==cave.Id&&r.Source==Planarian.Model.Database.Entities.RidgeWalker.CaveRevisionSource.Import));
    }

    [Fact]
    public async Task EntrancePreviewMatchesPersistedSemanticResultFromSamePlan()
    {
        await using var database=await fixture.CreateDatabaseAsync(nameof(EntrancePreviewMatchesPersistedSemanticResultFromSamePlan));
        var tenant=await IntegrationTestData.SeedTenantAsync(database,'a'); EntranceImportPlan plan; Planarian.Modules.Account.Import.Models.EntranceDryRun preview;
        await using(var db=database.CreateDbContext("a",tenant.AccountId))
        {
            var planner=new EntranceImportPlanner(db,db.RequestUser);
            await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.EntranceHeader+"\nMain,A01,1,true,35.123,-86.456,612,Survey Grade,20,Open,Wet,Sink,2026-08-01,Surveyor,Entrance description\n");
            plan=await planner.PlanAsync(csv,false); preview=Assert.Single(plan.CreatePreview());
            var snapshots=new CavePublishedSnapshotReader(db,db.RequestUser); var publisher=new ImportRevisionPublisher(db,db.RequestUser);
            await new EntranceImportExecutor(db,db.RequestUser,snapshots,publisher).ExecuteAsync(plan,"entrances.csv");
        }
        var planned=Assert.Single(plan.Entrances); await using var verify=database.CreateDbContext("a",tenant.AccountId);
        var entrance=await verify.Entrances.IgnoreQueryFilters().SingleAsync(e=>e.Id==planned.Id);
        Assert.Equal(1,preview.EntranceCountChange); Assert.Equal(preview.EntranceName,entrance.Name); Assert.Equal(preview.DecimalLatitude,entrance.Location.Y,6); Assert.Equal(preview.DecimalLongitude,entrance.Location.X,6); Assert.Equal(4326,entrance.Location.SRID);
        var cave=await verify.Caves.IgnoreQueryFilters().SingleAsync(c=>c.Id==tenant.CaveId); Assert.NotNull(cave.CurrentRevisionId);
    }
}
