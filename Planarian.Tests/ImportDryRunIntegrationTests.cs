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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaveDryRunPlanningAndPreviewPersistNothing(bool sync)
    {
        await using var database=await fixture.CreateDatabaseAsync($"{nameof(CaveDryRunPlanningAndPreviewPersistNothing)}_{sync}");
        var tenant=await IntegrationTestData.SeedTenantAsync(database,'a'); var before=await NormalizedDatabaseState.CaptureAllAsync(database);
        var writes = new RejectWriteCommandInterceptor();
        await using(var db=database.CreateDbContext("a",tenant.AccountId,writes))
        {
            var planner=new CaveImportPlanner(db,db.RequestUser);
            await using var csv=CsvStream(CaveHeader+"\nDry Run Cave,New County,NEW,99,AA,Alt,Needs Mapping,Mapper,100,25,10,2,Limestone,Mississippian,Plateau,None,Bats,2026-08-01,Reporter,false,Interesting,Dry narrative\n");
            var plan=await planner.PlanAsync(csv,sync); Assert.NotEmpty(plan.CreatePreview(true)); Assert.NotEmpty(plan.TagCreations); Assert.Single(plan.CountyCreations);
        }
        Assert.Equal(before,await NormalizedDatabaseState.CaptureAllAsync(database));
        Assert.Empty(writes.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EntranceDryRunPlanningAndPreviewPersistNothing(bool sync)
    {
        await using var database=await fixture.CreateDatabaseAsync($"{nameof(EntranceDryRunPlanningAndPreviewPersistNothing)}_{sync}");
        var tenant=await IntegrationTestData.SeedTenantAsync(database,'a'); var before=await NormalizedDatabaseState.CaptureAllAsync(database);
        var writes = new RejectWriteCommandInterceptor();
        await using(var db=database.CreateDbContext("a",tenant.AccountId,writes))
        {
            var planner=new EntranceImportPlanner(db,db.RequestUser);
            await using var csv=CsvStream(EntranceHeader+"\nMain,A01,1,true,35.1,-86.2,612,Brand New Quality,20,Open,Wet,Sink,2026-08-01,Surveyor,Description\n");
            var plan=await planner.PlanAsync(csv,sync); Assert.Single(plan.CreatePreview()); Assert.NotEmpty(plan.TagCreations);
        }
        Assert.Equal(before,await NormalizedDatabaseState.CaptureAllAsync(database));
        Assert.Empty(writes.Commands);
    }
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
