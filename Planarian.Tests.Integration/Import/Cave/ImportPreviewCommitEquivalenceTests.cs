using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Import.Models;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class ImportPreviewCommitEquivalenceTests(PostgresTestServer fixture):IClassFixture<PostgresTestServer>
{
    [Fact] public async Task CaveInsertPreviewMatchesCommit()=>await VerifyCaveAsync("insert",false,"Committed Cave,Committed County,COM,77,AA,Alt,Needs Mapping,Mapper,300,55,22,3,Limestone,Mississippian,Plateau,None,Bats,2026-08-01,Reporter,false,Interesting,Committed narrative",CaveImportAction.Insert);
    [Fact] public async Task CaveUpdatePreviewMatchesCommit()=>await VerifyCaveAsync("update",true,"Updated Cave,County A,A01,1,AA,Alt,Map,Mapper,300,55,22,3,Limestone,Mississippian,Plateau,None,Bats,2026-08-01,Reporter,true,Interesting,Updated narrative",CaveImportAction.Update);

    [Fact]
    public async Task CaveNoChangePreviewAndCommitAreEquivalent()
    {
        await using var d=await fixture.CreateDatabaseAsync(nameof(CaveNoChangePreviewAndCommitAreEquivalent));var t=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'a');await using var db=d.CreateDbContext("a",t.AccountId);var p=new CaveImportPlanningWorkflow(db,db.RequestUser);await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.CaveHeader+"\nCave A,County A,A01,1,AA,,,,,,,,,,,,,,,false,,\n");var plan=await p.PlanAsync(csv,true);var row=Assert.Single(plan.Caves);Assert.Equal(CaveImportAction.NoChange,row.Action);Assert.Equal("no change",Assert.Single(plan.CreatePreview(false)).Action);Assert.Empty(plan.CreatePreview(true));var before=await new CavePublishedSnapshotRepository(db,db.RequestUser).BuildAsync(t.CaveId);var revision=(await db.Caves.IgnoreQueryFilters().SingleAsync(x=>x.Id==t.CaveId)).CurrentRevisionId;await ExecuteCaves(db,plan,"no-change.csv");db.ChangeTracker.Clear();var after=await new CavePublishedSnapshotRepository(db,db.RequestUser).BuildAsync(t.CaveId);Assert.Equal(CaveSnapshotJson.Serialize(before),CaveSnapshotJson.Serialize(after));Assert.Equal(revision,(await db.Caves.IgnoreQueryFilters().SingleAsync(x=>x.Id==t.CaveId)).CurrentRevisionId);
    }

    [Fact]
    public async Task CaveSyncDeletionPreviewMatchesCommit()
    {
        await using var d=await fixture.CreateDatabaseAsync(nameof(CaveSyncDeletionPreviewMatchesCommit));var t=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'a');await using var db=d.CreateDbContext("a",t.AccountId);var p=new CaveImportPlanningWorkflow(db,db.RequestUser);await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.CaveHeader+"\nReplacement,Replacement County,REP,2,AA,,,,10,2,1,1,,,,,,,,false,,Replacement\n");var plan=await p.PlanAsync(csv,true);var deletion=Assert.Single(plan.Deletions);Assert.Equal(t.CaveId,deletion.CaveId);var preview=Assert.Single(plan.CreatePreview(true),x=>x.Action=="delete");Assert.Equal("Cave A",preview.CaveName);Assert.Equal("A01",preview.CountyCode);Assert.Equal(1,preview.CountyCaveNumber);var revisionsBefore=await db.CaveRevisions.CountAsync(x=>x.CaveId==t.CaveId);await ExecuteCaves(db,plan,"delete.csv");Assert.False(await db.Caves.IgnoreQueryFilters().AnyAsync(x=>x.Id==t.CaveId));var tombstone=await db.CaveRevisions.Where(x=>x.CaveId==t.CaveId).OrderByDescending(x=>x.CreatedOn).FirstAsync();Assert.Equal(revisionsBefore+1,await db.CaveRevisions.CountAsync(x=>x.CaveId==t.CaveId));Assert.Equal(CaveRevisionOperation.Delete,tombstone.Operation);
    }

    [Fact] public async Task EntranceInsertPreviewMatchesCommit()=>await VerifyEntranceAsync("insert",false,seedExisting:false,importPrimary:true);
    [Fact] public async Task EntranceNonSyncAppendPreviewMatchesCommit()=>await VerifyEntranceAsync("append",false,seedExisting:true,importPrimary:false);
    [Fact] public async Task EntranceSyncReplacementPreviewMatchesCommit()=>await VerifyEntranceAsync("replace",true,seedExisting:true,importPrimary:true);

    private async Task VerifyCaveAsync(string scenario,bool sync,string row,CaveImportAction expectedAction)
    {
        await using var d=await fixture.CreateDatabaseAsync($"preview_cave_{scenario}");var t=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'a');await using var db=d.CreateDbContext("a",t.AccountId);var p=new CaveImportPlanningWorkflow(db,db.RequestUser);await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.CaveHeader+'\n'+row+'\n');var plan=await p.PlanAsync(csv,sync);var planned=Assert.Single(plan.Caves);Assert.Equal(expectedAction,planned.Action);var preview=Assert.Single(plan.CreatePreview(true));await ExecuteCaves(db,plan,$"{scenario}.csv");db.ChangeTracker.Clear();var snapshot=await new CavePublishedSnapshotRepository(db,db.RequestUser).BuildAsync(planned.Id);AssertCavePreview(preview,snapshot);var cave=await db.Caves.IgnoreQueryFilters().SingleAsync(x=>x.Id==planned.Id);Assert.NotNull(cave.CurrentRevisionId);Assert.True(await db.CaveRevisions.AnyAsync(x=>x.Id==cave.CurrentRevisionId&&x.Source==CaveRevisionSource.Import));Assert.True(await db.CaveImportBatches.AnyAsync(x=>x.SourceFileName==$"{scenario}.csv"));
    }

    private async Task VerifyEntranceAsync(string scenario,bool sync,bool seedExisting,bool importPrimary)
    {
        await using var d=await fixture.CreateDatabaseAsync($"preview_entrance_{scenario}");var t=await TestDataScenarios.CreatePublishedCaveScenarioAsync(d,'a');if(seedExisting)await SeedPrimary(d,t);await using var db=d.CreateDbContext("a",t.AccountId);var p=new EntranceImportPlanningWorkflow(db,db.RequestUser);await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.EntranceHeader+$"\nImported,A01,1,{importPrimary.ToString().ToLowerInvariant()},35.123,-86.456,612,Survey Grade,20,Open,Wet,Sink,2026-08-01,Surveyor,Entrance description\n");var plan=await p.PlanAsync(csv,sync);var planned=Assert.Single(plan.Entrances);var preview=Assert.Single(plan.CreatePreview());await ExecuteEntrances(db,plan,$"{scenario}.csv");db.ChangeTracker.Clear();var snapshot=await new CavePublishedSnapshotRepository(db,db.RequestUser).BuildAsync(t.CaveId);var committed=Assert.Single(snapshot.Entrances,x=>x.Id==planned.Id);AssertEntrancePreview(preview,committed);Assert.Equal(preview.EntranceCountChange,snapshot.Entrances.Count-(seedExisting?1:0));if(sync)Assert.DoesNotContain(snapshot.Entrances,x=>x.Id=="existing00");var cave=await db.Caves.IgnoreQueryFilters().SingleAsync(x=>x.Id==t.CaveId);Assert.NotNull(cave.CurrentRevisionId);Assert.True(await db.CaveRevisions.AnyAsync(x=>x.Id==cave.CurrentRevisionId&&x.Source==CaveRevisionSource.Import));
    }

    private static void AssertCavePreview(CaveDryRunRecord p,CavePublishedSnapshotV1 s)
    {
        Assert.Equal(p.CaveName,s.Name);Assert.Equal(p.CountyCode,s.County.DisplayIdAtRevision);Assert.Equal(p.CountyName,s.County.NameAtRevision);Assert.Equal(p.CountyCaveNumber,s.CountyNumber);Assert.Equal(p.State,s.State.AbbreviationAtRevision);Assert.Equal(p.AlternateNames,s.AlternateNames);Assert.Equal(p.CaveLengthFeet,s.LengthFeet);Assert.Equal(p.CaveDepthFeet,s.DepthFeet);Assert.Equal(p.MaxPitDepthFeet,s.MaxPitDepthFeet);Assert.Equal(p.NumberOfPits,s.NumberOfPits);Assert.Equal(p.ReportedOnDate,s.ReportedOn);Assert.Equal(p.IsArchived,s.IsArchived);Assert.Equal(p.Narrative,s.Narrative);AssertRole(p.Geology,s,SnapshotTagRole.Geology);AssertRole(p.GeologicAges,s,SnapshotTagRole.GeologicAge);AssertRole(p.MapStatuses,s,SnapshotTagRole.MapStatus);AssertRole(p.PhysiographicProvinces,s,SnapshotTagRole.PhysiographicProvince);AssertRole(p.Archeology,s,SnapshotTagRole.Archeology);AssertRole(p.Biology,s,SnapshotTagRole.Biology);AssertRole(p.OtherTags,s,SnapshotTagRole.CaveOther);AssertRole(p.CartographerNames,s,SnapshotTagRole.Cartographer);AssertRole(p.ReportedByNames,s,SnapshotTagRole.CaveReportedBy);
    }
    private static void AssertEntrancePreview(EntranceDryRun p,CaveEntranceSnapshotV1 e){Assert.Equal(p.EntranceName,e.Name);Assert.Equal(p.IsPrimaryEntrance,e.IsPrimary);Assert.Equal(p.EntranceDescription,e.Description);Assert.Equal(p.DecimalLatitude,e.Latitude!.Value,6);Assert.Equal(p.DecimalLongitude,e.Longitude!.Value,6);Assert.Equal(p.EntranceElevationFt,e.Elevation!.Value,6);Assert.Equal(4326,e.Srid);Assert.Equal(p.LocationQuality,e.LocationQualityNameAtRevision);Assert.Equal(p.EntrancePitDepth,e.PitDepthFeet);Assert.Equal(p.ReportedOnDate,e.ReportedOn);AssertRole(p.EntranceStatuses,e,SnapshotTagRole.EntranceStatus);AssertRole(p.EntranceHydrology,e,SnapshotTagRole.EntranceHydrology);AssertRole(p.FieldIndication,e,SnapshotTagRole.FieldIndication);AssertRole(p.ReportedByNames,e,SnapshotTagRole.EntranceReportedBy);}
    private static void AssertRole(IEnumerable<string> expected,CavePublishedSnapshotV1 s,SnapshotTagRole role)=>Assert.Equal(expected.Order(),s.Tags.Where(x=>x.Role==role).Select(x=>x.NameAtRevision).Order());
    private static void AssertRole(IEnumerable<string> expected,CaveEntranceSnapshotV1 s,SnapshotTagRole role)=>Assert.Equal(expected.Order(),s.Tags.Where(x=>x.Role==role).Select(x=>x.NameAtRevision).Order());
    private static async Task SeedPrimary(PostgresTestDatabase d,PublishedCaveScenario t){await using var db=d.CreateDbContext("a",t.AccountId);var q=new TagType("Survey Grade",TagTypeKeyConstant.LocationQuality){Id=IdGenerator.Generate(),AccountId=t.AccountId,IsDefault=false};db.TagTypes.Add(q);await db.SaveChangesAsync();db.Entrances.Add(new Entrance{Id="existing00",CaveId=t.CaveId,LocationQualityTagId=q.Id,Name="Existing",IsPrimary=true,Location=new Point(new CoordinateZ(-86,35,500)){SRID=4326}});await db.SaveChangesAsync();}
    private static async Task ExecuteCaves(Planarian.Model.Database.PlanarianDbContext db,CaveImportPlan plan,string file){var r=new CavePublishedSnapshotRepository(db,db.RequestUser);await new CaveImportExecutionRepository(db,db.RequestUser,r,new CaveImportRevisionRepository(db,db.RequestUser)).ExecuteAsync(plan,file);}
    private static async Task ExecuteEntrances(Planarian.Model.Database.PlanarianDbContext db,EntranceImportPlan plan,string file){var r=new CavePublishedSnapshotRepository(db,db.RequestUser);await new EntranceImportExecutionRepository(db,db.RequestUser,r,new CaveImportRevisionRepository(db,db.RequestUser)).ExecuteAsync(plan,file);}
}
