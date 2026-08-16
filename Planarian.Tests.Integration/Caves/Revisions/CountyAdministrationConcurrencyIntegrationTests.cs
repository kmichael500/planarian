using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Import.Planning;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Concurrency;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CountyAdministrationConcurrencyIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task CaveWriterWinsThenWaitingStateMoveRejectsLiveReference()
    {
        var scenario = await CreateMoveScenarioAsync(nameof(CaveWriterWinsThenWaitingStateMoveRejectsLiveReference));
        await using var database = scenario.Database;
        var pause = new HoldCountyLockInterceptor("for update");
        await using var writer = database.CreateDbContext("writer", scenario.Cave.AccountId, pause);
        await CavePermissions.AuthenticateAsync(writer, scenario.Cave.AccountId);
        var write = IntegrationTestServices.For(writer).Caves.AddCave(scenario.Values, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var admin = database.CreateDbContext("admin", scenario.Cave.AccountId);
        await admin.Database.OpenConnectionAsync();
        var adminPid = await PostgresLockAssertions.BackendPidAsync(admin);
        var move = IntegrationTestServices.For(admin).Account.CreateOrUpdateCounty(scenario.OtherStateId,
            new CreateCountyVm { Name = "Target", CountyDisplayId = "Z99" }, scenario.TargetCountyId, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, scenario.Cave.AccountId, adminPid, move);

        pause.Resume.TrySetResult();
        await write;
        var failure = await Assert.ThrowsAsync<ApiException>(() => move);
        Assert.Equal(400, failure.StatusCode);
        Assert.Contains("Cannot move county to another state because it is in use.", failure.Message);

        await using var verify = database.CreateDbContext("verify", scenario.Cave.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == scenario.Cave.CaveId);
        var county = await verify.Counties.SingleAsync(row => row.Id == scenario.TargetCountyId);
        Assert.Equal(scenario.Cave.StateId, county.StateId);
        Assert.Equal(county.StateId, cave.StateId);
        Assert.Equal(county.Id, cave.CountyId);
        Assert.NotEqual(scenario.Cave.RevisionId, cave.CurrentRevisionId);
    }

    [Fact]
    public async Task CountyStateMoveWinsThenWaitingCaveWriterRejectsStalePair()
    {
        var scenario = await CreateMoveScenarioAsync(nameof(CountyStateMoveWinsThenWaitingCaveWriterRejectsStalePair));
        await using var database = scenario.Database;
        var pause = new HoldCountyLockInterceptor("for update");
        await using var admin = database.CreateDbContext("admin", scenario.Cave.AccountId, pause);
        var move = IntegrationTestServices.For(admin).Account.CreateOrUpdateCounty(scenario.OtherStateId,
            new CreateCountyVm { Name = "Target", CountyDisplayId = "Z99" }, scenario.TargetCountyId, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var writer = database.CreateDbContext("writer", scenario.Cave.AccountId);
        await CavePermissions.AuthenticateAsync(writer, scenario.Cave.AccountId);
        await writer.Database.OpenConnectionAsync();
        var writerPid = await PostgresLockAssertions.BackendPidAsync(writer);
        var write = IntegrationTestServices.For(writer).Caves.AddCave(scenario.Values, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, scenario.Cave.AccountId, writerPid, write);

        pause.Resume.TrySetResult();
        await move;
        var failure = await Assert.ThrowsAsync<ApiException>(() => write);
        Assert.Equal(400, failure.StatusCode);
        Assert.Contains("The selected County does not belong to the selected State.", failure.Message);

        await using var verify = database.CreateDbContext("verify", scenario.Cave.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == scenario.Cave.CaveId);
        Assert.Equal(scenario.Cave.CountyId, cave.CountyId);
        Assert.Equal(scenario.Cave.RevisionId, cave.CurrentRevisionId);
        Assert.Equal(scenario.OtherStateId,
            (await verify.Counties.SingleAsync(row => row.Id == scenario.TargetCountyId)).StateId);
    }

    [Fact]
    public async Task CaveImportReferenceLockMakesStateMoveWaitThenRejectLiveReference()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveImportReferenceLockMakesStateMoveWaitThenRejectLiveReference));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        const string otherStateId = "state0000z";
        const string targetCountyId = "county00az";
        await GlobalStateTestData.AddAsync(database, otherStateId, "State Z", "ZZ");
        await AddCountyAsync(database, account.AccountId, account.StateId, targetCountyId, "Target", "A99");

        CaveImportPlan plan;
        await using (var planner = database.CreateDbContext("planner", account.AccountId))
            plan = await new CaveImportTestHarness(planner, planner.RequestUser).PlanCsvAsync(
                ImportDryRunIntegrationTests.CaveHeader + "\n" +
                "Imported,Target,A99,1,AA,,,,,,,1,,,,,,,,false,,\n", false);

        var pause = new HoldCountyLockInterceptor("for key share");
        await using var importer = database.CreateDbContext("importer", account.AccountId, pause);
        var import = new CaveImportTestHarness(importer, importer.RequestUser).ExecuteAsync(plan, "county-lock.csv");
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var admin = database.CreateDbContext("admin", account.AccountId);
        await admin.Database.OpenConnectionAsync();
        var adminPid = await PostgresLockAssertions.BackendPidAsync(admin);
        var move = IntegrationTestServices.For(admin).Account.CreateOrUpdateCounty(otherStateId,
            new CreateCountyVm { Name = "Target", CountyDisplayId = "Z99" }, targetCountyId, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, account.AccountId, adminPid, move);

        pause.Resume.TrySetResult();
        await import;
        var failure = await Assert.ThrowsAsync<ApiException>(() => move);
        Assert.Equal(400, failure.StatusCode);

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Name == "Imported");
        Assert.Equal(targetCountyId, cave.CountyId);
        Assert.Equal(account.StateId, cave.StateId);
        Assert.NotNull(cave.CurrentRevisionId);
        Assert.True(await verify.CaveImportBatches.AnyAsync(batch => batch.SourceFileName == "county-lock.csv"));
    }

    [Fact]
    public async Task CountyStateMoveWinsThenWaitingCaveImportRejectsStaleMetadataWithoutWrites()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CountyStateMoveWinsThenWaitingCaveImportRejectsStaleMetadataWithoutWrites));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        const string otherStateId = "state0000z";
        const string targetCountyId = "county00az";
        await GlobalStateTestData.AddAsync(database, otherStateId, "State Z", "ZZ");
        await AddCountyAsync(database, account.AccountId, account.StateId, targetCountyId, "Target", "A99");

        CaveImportPlan plan;
        await using (var planner = database.CreateDbContext("planner", account.AccountId))
            plan = await new CaveImportTestHarness(planner, planner.RequestUser).PlanCsvAsync(
                ImportDryRunIntegrationTests.CaveHeader + "\n" +
                "Imported,Target,A99,1,AA,,,,,,,1,,,,,,,,false,,\n", false);

        var pause = new HoldCountyLockInterceptor("for update");
        await using var admin = database.CreateDbContext("admin", account.AccountId, pause);
        var move = IntegrationTestServices.For(admin).Account.CreateOrUpdateCounty(otherStateId,
            new CreateCountyVm { Name = "Target", CountyDisplayId = "Z99" }, targetCountyId, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var importer = database.CreateDbContext("importer", account.AccountId);
        await importer.Database.OpenConnectionAsync();
        var importPid = await PostgresLockAssertions.BackendPidAsync(importer);
        var import = new CaveImportTestHarness(importer, importer.RequestUser).ExecuteAsync(plan, "stale-county.csv");
        await PostgresLockAssertions.AssertBlockedAsync(database, account.AccountId, importPid, import);

        pause.Resume.TrySetResult();
        await move;
        await Assert.ThrowsAsync<ImportPlanConcurrencyException>(() => import);

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.Equal(otherStateId, (await verify.Counties.SingleAsync(row => row.Id == targetCountyId)).StateId);
        Assert.False(await verify.Caves.IgnoreQueryFilters().AnyAsync(row => row.Name == "Imported"));
        Assert.False(await verify.CaveImportBatches.AnyAsync(batch => batch.SourceFileName == "stale-county.csv"));
        Assert.Empty(await verify.CaveRevisions.ToListAsync());
    }

    [Fact]
    public async Task CaveWriterMakesCountyDeleteWaitThenForeignKeyFailureIsControlled()
    {
        var scenario = await CreateMoveScenarioAsync(nameof(CaveWriterMakesCountyDeleteWaitThenForeignKeyFailureIsControlled));
        await using var database = scenario.Database;
        var pause = new HoldCountyLockInterceptor("for update");
        await using var writer = database.CreateDbContext("writer", scenario.Cave.AccountId, pause);
        await CavePermissions.AuthenticateAsync(writer, scenario.Cave.AccountId);
        var write = IntegrationTestServices.For(writer).Caves.AddCave(scenario.Values, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var admin = database.CreateDbContext("admin", scenario.Cave.AccountId);
        await admin.Database.OpenConnectionAsync();
        var adminPid = await PostgresLockAssertions.BackendPidAsync(admin);
        var delete = IntegrationTestServices.For(admin).Account.DeleteCounties([scenario.TargetCountyId], default);
        await PostgresLockAssertions.AssertBlockedAsync(database, scenario.Cave.AccountId, adminPid, delete);

        pause.Resume.TrySetResult();
        await write;
        var failure = await Assert.ThrowsAsync<ApiException>(() => delete);
        Assert.Equal(400, failure.StatusCode);
        Assert.Contains("Cannot delete county because it is in use.", failure.Message);

        await using var verify = database.CreateDbContext("verify", scenario.Cave.AccountId);
        Assert.True(await verify.Counties.AnyAsync(row => row.Id == scenario.TargetCountyId));
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == scenario.Cave.CaveId);
        Assert.Equal(scenario.TargetCountyId, cave.CountyId);
        Assert.NotEqual(scenario.Cave.RevisionId, cave.CurrentRevisionId);
    }

    [Fact]
    public async Task AlreadyReferencedCountyDeleteIsControlledAndPreservesPublishedCave()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AlreadyReferencedCountyDeleteIsControlledAndPreservesPublishedCave));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await using var admin = database.CreateDbContext("admin", cave.AccountId);
        var failure = await Assert.ThrowsAsync<ApiException>(() =>
            IntegrationTestServices.For(admin).Account.DeleteCounties([cave.CountyId], default));
        Assert.Equal(400, failure.StatusCode);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.True(await verify.Counties.AnyAsync(row => row.Id == cave.CountyId));
        var persisted = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal(cave.RevisionId, persisted.CurrentRevisionId);
    }

    private async Task<MoveScenario> CreateMoveScenarioAsync(string databaseName)
    {
        var database = await fixture.CreateDatabaseAsync(databaseName);
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var location = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            Planarian.Model.Shared.TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        const string targetCountyId = "county00az";
        const string otherStateId = "state0000z";
        await GlobalStateTestData.AddAsync(database, otherStateId, "State Z", "ZZ");
        await AddCountyAsync(database, cave.AccountId, cave.StateId, targetCountyId, "Target", "A99");
        await CavePermissions.GrantManagerAsync(database, cave, "writer");
        var values = PublishableValues(cave, location.Id, "Moved");
        values.CountyId = targetCountyId;
        values.CountyNumber = 2;
        return new MoveScenario(database, cave, targetCountyId, otherStateId, values);
    }

    private static async Task AddCountyAsync(PostgresTestDatabase database, string accountId, string stateId,
        string countyId, string name, string displayId)
    {
        await using var db = database.CreateDbContext("county-seed", accountId);
        db.Counties.Add(new County
            { Id = countyId, AccountId = accountId, StateId = stateId, Name = name, DisplayId = displayId });
        await db.SaveChangesAsync();
    }

    private sealed record MoveScenario(PostgresTestDatabase Database, PublishedCaveTestData Cave,
        string TargetCountyId, string OtherStateId, Planarian.Modules.Caves.Models.AddCaveVm Values);
}

internal sealed class HoldCountyLockInterceptor(string lockClause) : DbCommandInterceptor
{
    private int _paused;
    public TaskCompletionSource LockAcquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
        CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("from \"Counties\"", StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(lockClause, StringComparison.OrdinalIgnoreCase) &&
            Interlocked.Exchange(ref _paused, 1) == 0)
        {
            LockAcquired.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
        }
        return result;
    }
}
