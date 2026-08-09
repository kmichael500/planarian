using System.Text;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
        var writes = new RejectWriteCommandInterceptor();
        await using(var db=database.CreateDbContext("a",tenant.AccountId,writes))
        {
            var planner=new CaveImportPlanner(db,db.RequestUser);
            await using var csv=CsvStream(CaveHeader+"\nDry Run Cave,New County,NEW,99,AA,Alt,Needs Mapping,Mapper,100,25,10,2,Limestone,Mississippian,Plateau,None,Bats,2026-08-01,Reporter,false,Interesting,Dry narrative\n");
            var plan=await planner.PlanAsync(csv,false); Assert.Single(plan.CreatePreview(true)); Assert.NotEmpty(plan.TagCreations); Assert.Single(plan.CountyCreations);
        }
        Assert.Equal(before,await Capture(database));
        Assert.Empty(writes.Commands);
    }

    [Fact]
    public async Task EntranceDryRunPlanningAndPreviewPersistNothing()
    {
        await using var database=await fixture.CreateDatabaseAsync(nameof(EntranceDryRunPlanningAndPreviewPersistNothing));
        var tenant=await IntegrationTestData.SeedTenantAsync(database,'a'); var before=await Capture(database);
        var writes = new RejectWriteCommandInterceptor();
        await using(var db=database.CreateDbContext("a",tenant.AccountId,writes))
        {
            var planner=new EntranceImportPlanner(db,db.RequestUser);
            await using var csv=CsvStream(EntranceHeader+"\nMain,A01,1,true,35.1,-86.2,612,Brand New Quality,20,Open,Wet,Sink,2026-08-01,Surveyor,Description\n");
            var plan=await planner.PlanAsync(csv,false); Assert.Single(plan.CreatePreview()); Assert.NotEmpty(plan.TagCreations);
        }
        Assert.Equal(before,await Capture(database));
        Assert.Empty(writes.Commands);
    }

    private static async Task<State> Capture(PostgresTestDatabase database)
    {
        await using var connection=new NpgsqlConnection(database.ConnectionString); await connection.OpenAsync();
        await using var command=connection.CreateCommand(); command.CommandText="""
        select (select count(*) from "TagTypes"),(select count(*) from "Counties"),(select count(*) from "AccountStates"),(select count(*) from "Caves"),
        (select count(*) from "GeologyTags")+(select count(*) from "GeologicAgeTags")+(select count(*) from "MapStatusTags")+(select count(*) from "PhysiographicProvinceTags")+(select count(*) from "ArcheologyTags")+(select count(*) from "BiologyTags")+(select count(*) from "CaveOtherTags")+(select count(*) from "CartographerNameTags")+(select count(*) from "CaveReportedByNameTags"),
        (select count(*) from "Entrances"),(select count(*) from "EntranceStatusTags")+(select count(*) from "EntranceHydrologyTags")+(select count(*) from "FieldIndicationTags")+(select count(*) from "EntranceReportedByNameTags")+(select count(*) from "EntranceOtherTag"),
        (select count(*) from "CaveRevisions"),(select count(*) from "CaveImportBatches")
        """;
        await using var r=await command.ExecuteReaderAsync(); Assert.True(await r.ReadAsync()); return new State(r.GetInt64(0),r.GetInt64(1),r.GetInt64(2),r.GetInt64(3),r.GetInt64(4),r.GetInt64(5),r.GetInt64(6),r.GetInt64(7),r.GetInt64(8));
    }
    private sealed record State(long Tags,long Counties,long AccountStates,long Caves,long CaveTags,long Entrances,long EntranceTags,long Revisions,long Batches);
}

/// <summary>Planning is read-only: a preview must never hide a write behind rollback.</summary>
internal sealed class RejectWriteCommandInterceptor : DbCommandInterceptor
{
    private readonly List<string> _commands = [];
    public IReadOnlyList<string> Commands => _commands;

    private void Check(DbCommand command)
    {
        var sql = command.CommandText.TrimStart();
        if (!sql.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) &&
            !sql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) &&
            !sql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) &&
            !sql.StartsWith("ALTER", StringComparison.OrdinalIgnoreCase) &&
            !sql.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase) &&
            !sql.StartsWith("DROP", StringComparison.OrdinalIgnoreCase)) return;
        _commands.Add(sql);
        throw new InvalidOperationException($"Import planning issued a write/DDL command: {sql[..Math.Min(sql.Length, 80)]}");
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) { Check(command); return result; }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result) { Check(command); return result; }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) { Check(command); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) { Check(command); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default) { Check(command); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) { Check(command); return ValueTask.FromResult(result); }
}
