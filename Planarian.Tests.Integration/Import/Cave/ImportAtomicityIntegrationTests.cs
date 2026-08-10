using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class ImportAtomicityIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task CaveSecondRecordLateFailureRollsBackCompleteState()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveSecondRecordLateFailureRollsBackCompleteState));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        CaveImportPlan plan;
        await using (var planningDb = database.CreateDbContext("a", tenant.AccountId))
        {
            var import = new CaveImportTestHarness(planningDb, planningDb.RequestUser);
            var csv = ImportDryRunIntegrationTests.CaveHeader +
                "\nRecord A,New County,NEW,50,AA,,,,100,20,5,1,Rollback Geology,,,,,,,false,,A" +
                "\nRecord B,New County,NEW,51,AA,,,,110,21,6,2,Rollback Geology,,,,,,,false,,B\n";
            plan = await import.PlanCsvAsync(csv, syncExisting: false);
        }

        Assert.Equal(2, plan.Caves.Count);
        Assert.Contains(plan.TagCreations, creation => creation.Name == "Rollback Geology");
        Assert.Single(plan.CountyCreations);
        var before = await NormalizedDatabaseState.CaptureAllAsync(database);
        var fault = new FailAtRevisionPublicationInterceptor();

        // Act
        await using var executionDb = database.CreateDbContext("a", tenant.AccountId, fault);
        var execution = new CaveImportTestHarness(executionDb, executionDb.RequestUser);
        var error = await Assert.ThrowsAsync<DbUpdateException>(
            () => execution.ExecuteAsync(plan, "rollback-cave.csv"));

        // Assert
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Contains("deterministic late failure", error.InnerException!.Message);
        Assert.True(fault.SawCaveWriteBeforeFailure);
        Assert.Equal(before, await NormalizedDatabaseState.CaptureAllAsync(database));
    }

    [Fact]
    public async Task EntranceSecondRecordLateFailureRollsBackCompleteState()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(EntranceSecondRecordLateFailureRollsBackCompleteState));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await TestDataBuilder.AddCaveAsync(database, tenant, caveId: "secondcav0", countyNumber: 2,
            name: "Second");
        await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade");

        EntranceImportPlan plan;
        await using (var planningDb = database.CreateDbContext("a", tenant.AccountId))
        {
            var import = new EntranceImportTestHarness(planningDb, planningDb.RequestUser);
            var csv = ImportDryRunIntegrationTests.EntranceHeader +
                "\nRecord A,A01,1,true,35,-86,500,Survey Grade,1,Rollback Status,,,,,,A" +
                "\nRecord B,A01,2,true,35.1,-86.1,510,Survey Grade,2,Rollback Status,,,,,,B\n";
            plan = await import.PlanCsvAsync(csv, syncExisting: false);
        }

        Assert.Equal(2, plan.Entrances.Count);
        Assert.Contains(plan.TagCreations, creation => creation.Name == "Rollback Status");
        var before = await NormalizedDatabaseState.CaptureAllAsync(database);
        var fault = new FailAtRevisionPublicationInterceptor();

        // Act
        await using var executionDb = database.CreateDbContext("a", tenant.AccountId, fault);
        var execution = new EntranceImportTestHarness(executionDb, executionDb.RequestUser);
        var error = await Assert.ThrowsAsync<DbUpdateException>(
            () => execution.ExecuteAsync(plan, "rollback-ent.csv"));

        // Assert
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Contains("deterministic late failure", error.InnerException!.Message);
        Assert.True(fault.SawEntranceWriteBeforeFailure);
        Assert.Equal(before, await NormalizedDatabaseState.CaptureAllAsync(database));
    }
}

/// <summary>Fails after imported relational rows have been written, immediately before revision publication.</summary>
internal sealed class FailAtRevisionPublicationInterceptor : DbCommandInterceptor
{
    public bool SawCaveWriteBeforeFailure { get; private set; }
    public bool SawEntranceWriteBeforeFailure { get; private set; }

    private void Inspect(DbCommand command)
    {
        var sql = command.CommandText;
        if (sql.Contains("INSERT INTO \"Caves\"", StringComparison.Ordinal))
            SawCaveWriteBeforeFailure = true;
        if (sql.Contains("INSERT INTO \"Entrances\"", StringComparison.Ordinal))
            SawEntranceWriteBeforeFailure = true;
        if (sql.Contains("INSERT INTO \"CaveRevisions\"", StringComparison.Ordinal))
            throw new InvalidOperationException("Injected deterministic late failure at revision publication.");
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Inspect(command);
        return result;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Inspect(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Inspect(command);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Inspect(command);
        return ValueTask.FromResult(result);
    }
}
