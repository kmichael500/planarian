using System.Text;
using Npgsql;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class ImportDryRunIntegrationTests(PostgresIntegrationFixture fixture) : IClassFixture<PostgresIntegrationFixture>
{
    internal const string CaveHeader="CaveName,CountyName,CountyCode,CountyCaveNumber,State,AlternateNames,MapStatuses,CartographerNames,CaveLengthFt,CaveDepthFt,MaxPitDepthFt,NumberOfPits,Geology,GeologicAges,PhysiographicProvinces,Archeology,Biology,ReportedOnDate,ReportedByNames,IsArchived,OtherTags,Narrative";
    internal const string EntranceHeader="EntranceName,CountyCode,CountyCaveNumber,IsPrimaryEntrance,DecimalLatitude,DecimalLongitude,EntranceElevationFt,LocationQuality,EntrancePitDepth,EntranceStatuses,EntranceHydrology,FieldIndication,ReportedOnDate,ReportedByNames,EntranceDescription";
    internal static MemoryStream CsvStream(string csv)=>new(Encoding.UTF8.GetBytes(csv));

    [Fact]
    public async Task CaveDryRunPlanningAndPreviewPersistNothing()
    {
        await using var database=await fixture.CreateDatabaseAsync(nameof(CaveDryRunPlanningAndPreviewPersistNothing));
        var tenant=await IntegrationTestData.SeedTenantAsync(database,'a'); var before=await Capture(database);
        await using(var db=database.CreateDbContext("a",tenant.AccountId))
        {
            var planner=new CaveImportPlanner(db,db.RequestUser);
            await using var csv=CsvStream(CaveHeader+"\nDry Run Cave,New County,NEW,99,AA,Alt,Needs Mapping,Mapper,100,25,10,2,Limestone,Mississippian,Plateau,None,Bats,2026-08-01,Reporter,false,Interesting,Dry narrative\n");
            var plan=await planner.PlanAsync(csv,false); Assert.Single(plan.CreatePreview(true)); Assert.NotEmpty(plan.TagCreations); Assert.Single(plan.CountyCreations);
        }
        Assert.Equal(before,await Capture(database));
    }

    [Fact]
    public async Task EntranceDryRunPlanningAndPreviewPersistNothing()
    {
        await using var database=await fixture.CreateDatabaseAsync(nameof(EntranceDryRunPlanningAndPreviewPersistNothing));
        var tenant=await IntegrationTestData.SeedTenantAsync(database,'a'); var before=await Capture(database);
        await using(var db=database.CreateDbContext("a",tenant.AccountId))
        {
            var planner=new EntranceImportPlanner(db,db.RequestUser);
            await using var csv=CsvStream(EntranceHeader+"\nMain,A01,1,true,35.1,-86.2,612,Brand New Quality,20,Open,Wet,Sink,2026-08-01,Surveyor,Description\n");
            var plan=await planner.PlanAsync(csv,false); Assert.Single(plan.CreatePreview()); Assert.NotEmpty(plan.TagCreations);
        }
        Assert.Equal(before,await Capture(database));
    }

    private static async Task<State> Capture(PostgresTestDatabase database)
    {
        await using var connection=new NpgsqlConnection(database.ConnectionString); await connection.OpenAsync();
        await using var command=connection.CreateCommand(); command.CommandText="""
        select (select count(*) from "TagTypes"),(select count(*) from "Counties"),(select count(*) from "AccountStates"),(select count(*) from "Caves"),
        (select count(*) from "GeologyTags")+(select count(*) from "GeologicAgeTags")+(select count(*) from "MapStatusTags")+(select count(*) from "PhysiographicProvinceTags")+(select count(*) from "ArcheologyTags")+(select count(*) from "BiologyTags")+(select count(*) from "CaveOtherTags")+(select count(*) from "CartographerNameTags")+(select count(*) from "CaveReportedByNameTags"),
        (select count(*) from "Entrances"),(select count(*) from "EntranceStatusTags")+(select count(*) from "EntranceHydrologyTags")+(select count(*) from "FieldIndicationTags")+(select count(*) from "EntranceReportedByNameTags")+(select count(*) from "EntranceOtherTags"),
        (select count(*) from "CaveRevisions"),(select count(*) from "CaveImportBatches")
        """;
        await using var r=await command.ExecuteReaderAsync(); Assert.True(await r.ReadAsync()); return new State(r.GetInt64(0),r.GetInt64(1),r.GetInt64(2),r.GetInt64(3),r.GetInt64(4),r.GetInt64(5),r.GetInt64(6),r.GetInt64(7),r.GetInt64(8));
    }
    private sealed record State(long Tags,long Counties,long AccountStates,long Caves,long CaveTags,long Entrances,long EntranceTags,long Revisions,long Batches);
}
