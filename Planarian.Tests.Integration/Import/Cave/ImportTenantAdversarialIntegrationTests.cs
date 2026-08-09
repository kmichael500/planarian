using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class ImportTenantAdversarialIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task CaveSyncCannotUpdateForeignCollidingCave()
    {
        await using var d=await fixture.CreateDatabaseAsync(nameof(CaveSyncCannotUpdateForeignCollidingCave)); var a=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'a'); var b=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'b');
        await MakeBVisibleKeyCollide(d,b,"A01"); var before=await NormalizedDatabaseState.CaptureTenantAsync(d,b.AccountId); await using var db=d.CreateDbContext("a",a.AccountId); var p=new CaveImportPlanningWorkflow(db,db.RequestUser);
        await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.CaveHeader+"\nA Updated,County A,A01,1,AA,,,,100,20,5,1,,,,,,2026-08-01,,false,,A only\n"); var plan=await p.PlanAsync(csv,true);
        Assert.DoesNotContain(plan.Caves,c=>c.Id==b.CaveId); await ExecuteCaves(db,plan,"collision.csv"); Assert.Equal(before,await NormalizedDatabaseState.CaptureTenantAsync(d,b.AccountId));
    }

    [Fact]
    public async Task CaveSyncCannotDeleteForeignCave()
    {
        await using var d=await fixture.CreateDatabaseAsync(nameof(CaveSyncCannotDeleteForeignCave)); var a=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'a'); var b=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'b');
        var before=await NormalizedDatabaseState.CaptureTenantAsync(d,b.AccountId); await using var db=d.CreateDbContext("a",a.AccountId); var p=new CaveImportPlanningWorkflow(db,db.RequestUser);
        await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.CaveHeader+"\nReplacement,Replacement,REP,2,AA,,,,10,2,1,1,,,,,,,,false,,\n"); var plan=await p.PlanAsync(csv,true);
        Assert.DoesNotContain(plan.Deletions,x=>x.CaveId==b.CaveId); await ExecuteCaves(db,plan,"delete.csv"); Assert.Equal(before,await NormalizedDatabaseState.CaptureTenantAsync(d,b.AccountId));
    }

    [Fact]
    public async Task EntranceCannotAssociateToForeignCave()
    {
        await using var d=await fixture.CreateDatabaseAsync(nameof(EntranceCannotAssociateToForeignCave)); var a=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'a'); var b=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'b');
        var before=await NormalizedDatabaseState.CaptureTenantAsync(d,b.AccountId); await using var db=d.CreateDbContext("a",a.AccountId); var p=new EntranceImportPlanningWorkflow(db,db.RequestUser);
        await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.EntranceHeader+"\nForeign,B01,1,true,35,-86,500,Survey Grade,0,Open,,,,,,\n"); await Assert.ThrowsAsync<ApiException>(()=>p.PlanAsync(csv,false)); Assert.Equal(before,await NormalizedDatabaseState.CaptureTenantAsync(d,b.AccountId));
    }

    [Fact]
    public async Task ForeignPrimaryEntranceDoesNotAffectAccountAPrimaryCount()
    {
        await using var d=await fixture.CreateDatabaseAsync(nameof(ForeignPrimaryEntranceDoesNotAffectAccountAPrimaryCount)); var a=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'a'); var b=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'b'); await SeedPrimary(d,b,"bprimary00"); await MakeBVisibleKeyCollide(d,b,"A01");
        var before=await NormalizedDatabaseState.CaptureTenantAsync(d,b.AccountId); await using var db=d.CreateDbContext("a",a.AccountId); var p=new EntranceImportPlanningWorkflow(db,db.RequestUser); await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.EntranceHeader+"\nA Primary,A01,1,true,35,-86,500,Survey Grade,0,Open,,,,,,\n");
        var plan=await p.PlanAsync(csv,false); Assert.Single(plan.Entrances); await ExecuteEntrances(db,plan,"primary.csv"); Assert.Equal(before,await NormalizedDatabaseState.CaptureTenantAsync(d,b.AccountId));
    }

    [Fact]
    public async Task EntranceSyncDoesNotDeleteForeignEntrancesOrTags()
    {
        await using var d=await fixture.CreateDatabaseAsync(nameof(EntranceSyncDoesNotDeleteForeignEntrancesOrTags)); var a=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'a'); var b=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'b'); await SeedPrimary(d,a,"aprimary00"); await SeedPrimary(d,b,"bprimary00");
        var before=await NormalizedDatabaseState.CaptureTenantAsync(d,b.AccountId); await using var db=d.CreateDbContext("a",a.AccountId); var p=new EntranceImportPlanningWorkflow(db,db.RequestUser); await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.EntranceHeader+"\nReplacement,A01,1,true,35.2,-86.2,520,Survey Grade,0,Open,Wet,Sink,2026-08-01,Surveyor,Replacement\n"); var plan=await p.PlanAsync(csv,true); await ExecuteEntrances(db,plan,"sync.csv");
        Assert.Equal(before,await NormalizedDatabaseState.CaptureTenantAsync(d,b.AccountId));
    }

    [Fact]
    public async Task ForeignCustomTagIsNeverReusable()
    {
        await using var d=await fixture.CreateDatabaseAsync(nameof(ForeignCustomTagIsNeverReusable)); var a=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'a'); var b=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'b'); string foreign;
        await using(var bb=d.CreateDbContext("b",b.AccountId)){var t=new TagType("Foreign Status",TagTypeKeyConstant.EntranceStatus){Id=IdGenerator.Generate(),AccountId=b.AccountId,IsDefault=false};bb.TagTypes.Add(t);await bb.SaveChangesAsync();foreign=t.Id;}var before=await NormalizedDatabaseState.CaptureTenantAsync(d,b.AccountId);
        await using var db=d.CreateDbContext("a",a.AccountId); var p=new EntranceImportPlanningWorkflow(db,db.RequestUser); await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.EntranceHeader+"\nA Entrance,A01,1,true,35,-86,500,Survey Grade,0,Foreign Status,,,,,,\n"); var plan=await p.PlanAsync(csv,false); Assert.Contains(plan.TagCreations,t=>t.Name=="Foreign Status"&&t.Id!=foreign); await ExecuteEntrances(db,plan,"tag.csv"); Assert.False(await db.EntranceStatusTags.AnyAsync(t=>t.TagTypeId==foreign));Assert.Equal(before,await NormalizedDatabaseState.CaptureTenantAsync(d,b.AccountId));
    }

    private static async Task ExecuteCaves(Planarian.Model.Database.PlanarianDbContext db,CaveImportPlan plan,string file){var r=new CavePublishedSnapshotRepository(db,db.RequestUser);await new CaveImportExecutionRepository(db,db.RequestUser,r,new CaveImportRevisionRepository(db,db.RequestUser)).ExecuteAsync(plan,file);}
    private static async Task ExecuteEntrances(Planarian.Model.Database.PlanarianDbContext db,EntranceImportPlan plan,string file){var r=new CavePublishedSnapshotRepository(db,db.RequestUser);await new EntranceImportExecutionRepository(db,db.RequestUser,r,new CaveImportRevisionRepository(db,db.RequestUser)).ExecuteAsync(plan,file);}
    private static async Task MakeBVisibleKeyCollide(PostgresTestDatabase d,PublishedCaveScenario b,string code){await using var db=d.CreateDbContext("b",b.AccountId);var c=await db.Counties.SingleAsync(x=>x.Id==b.CountyId);c.DisplayId=code;await db.SaveChangesAsync();}
    private static async Task SeedPrimary(PostgresTestDatabase d,PublishedCaveScenario t,string id){await using var db=d.CreateDbContext("seed",t.AccountId);var q=new TagType("Survey Grade",TagTypeKeyConstant.LocationQuality){Id=IdGenerator.Generate(),AccountId=t.AccountId,IsDefault=false};var s=new TagType("Open",TagTypeKeyConstant.EntranceStatus){Id=IdGenerator.Generate(),AccountId=t.AccountId,IsDefault=false};db.TagTypes.AddRange(q,s);await db.SaveChangesAsync();db.Entrances.Add(new Entrance{Id=id,CaveId=t.CaveId,LocationQualityTagId=q.Id,IsPrimary=true,Location=new Point(new CoordinateZ(-86,35,500)){SRID=4326}});await db.SaveChangesAsync();db.EntranceStatusTags.Add(new EntranceStatusTag{Id=IdGenerator.Generate(),EntranceId=id,TagTypeId=s.Id});await db.SaveChangesAsync();}
}
