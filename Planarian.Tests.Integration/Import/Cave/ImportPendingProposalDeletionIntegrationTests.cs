using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Caves.Revisions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Import.Planning;
using Planarian.Tests.Integration.Caves.Revisions;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;

namespace Planarian.Tests;

public sealed class ImportPendingProposalDeletionIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task SyncDeletionWithPendingProposalRollsBackEntireImportAndPreservesStaging()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(SyncDeletionWithPendingProposalRollsBackEntireImportAndPreservesStaging));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var fileType = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.File, "Other", "filetype0a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");

        string requestId;
        string stagedFileId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, cave.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateAsync(cave.CaveId,
                CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Pending proposal"),
                cave.RevisionId, default);
            await using var stream = new MemoryStream([1, 2, 3]);
            stagedFileId = (await contributor.Services.StageRequestFileForTestAsync(
                requestId, stream, "pending.pdf", null, default)).Id;
        }

        CaveImportPlan plan;
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
        {
            var planningHarness = new CaveImportTestHarness(planning, planning.RequestUser);
            plan = await planningHarness.PlanCsvAsync(ImportDryRunIntegrationTests.CaveHeader +
                "\nReplacement,Replacement County,REP,2,AA,,,,10,2,1,1,,,,,,,,false,,\n", true);
        }
        Assert.Contains(plan.Deletions, deletion => deletion.CaveId == cave.CaveId);

        await using (var execution = database.CreateDbContext("executor", cave.AccountId))
        {
            var harness = new CaveImportTestHarness(execution, execution.RequestUser);
            await Assert.ThrowsAsync<ImportPlanConcurrencyException>(
                () => harness.ExecuteAsync(plan, "protected-delete.csv"));
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(row => row.Id == cave.CaveId));
        Assert.Equal(cave.RevisionId, await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync());
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(request => request.Id == requestId)).Status);
        Assert.True(await verify.CaveProposalVersions.AnyAsync(version => version.ChangeRequestId == requestId));
        Assert.True(await verify.CaveChangeRequestStagedFiles.AnyAsync(link =>
            link.ChangeRequestId == requestId && link.FileId == stagedFileId));
        Assert.True(await verify.Files.IgnoreQueryFilters().AnyAsync(file => file.Id == stagedFileId));
        Assert.Empty(await verify.CaveImportBatches.ToListAsync());
        Assert.Empty(await verify.CaveRevisions.Where(revision => revision.ImportBatchId != null).ToListAsync());
    }

    [Fact]
    public async Task ConcurrentRequestCreationWinsThenWaitingSyncDeletionSeesPendingRequest()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ConcurrentRequestCreationWinsThenWaitingSyncDeletionSeesPendingRequest));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        CaveImportPlan plan;
        await using (var planning = database.CreateDbContext("planner", cave.AccountId))
        {
            var harness = new CaveImportTestHarness(planning, planning.RequestUser);
            plan = await harness.PlanCsvAsync(ImportDryRunIntegrationTests.CaveHeader +
                "\nReplacement,Replacement County,REP,2,AA,,,,10,2,1,1,,,,,,,,false,,\n", true);
        }

        await using var contributor = database.CreateDbContext("contributor", cave.AccountId);
        await CavePermissions.EnsureAccountUserAsync(contributor, cave.AccountId);
        await contributor.RequestUser.Initialize(cave.AccountId, contributor.RequestUser.Id);
        var caveLockAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var createRequest = new CaveChangeRequestRepository(contributor, contributor.RequestUser).CreateAsync(
            cave.CaveId, cave.RevisionId, async _ =>
            {
                caveLockAcquired.TrySetResult();
                await finishRequest.Task;
                return CaveChangeRequestTestSupport.Proposal(cave, "Concurrent pending request");
            }, default);
        var requestStart = await Task.WhenAny(caveLockAcquired.Task, createRequest)
            .WaitAsync(TimeSpan.FromSeconds(15));
        if (requestStart == createRequest) await createRequest;
        await caveLockAcquired.Task;

        await using var execution = database.CreateDbContext("executor", cave.AccountId);
        await execution.Database.OpenConnectionAsync();
        int backendPid;
        await using (var pidCommand = execution.Database.GetDbConnection().CreateCommand())
        {
            pidCommand.CommandText = "select pg_backend_pid()";
            backendPid = Convert.ToInt32(await pidCommand.ExecuteScalarAsync());
        }
        var executionHarness = new CaveImportTestHarness(execution, execution.RequestUser);
        var import = executionHarness.ExecuteAsync(plan, "concurrent-protected-delete.csv");
        await WaitForLockWaitAsync(database, cave.AccountId, backendPid);

        finishRequest.TrySetResult();
        var requestId = await createRequest;
        await Assert.ThrowsAsync<ImportPlanConcurrencyException>(() => import);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(row => row.Id == cave.CaveId));
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(request => request.Id == requestId)).Status);
        Assert.Empty(await verify.CaveImportBatches.ToListAsync());
    }

    private static async Task WaitForLockWaitAsync(PostgresTestDatabase database, string accountId, int backendPid)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var observer = database.CreateDbContext("lock-observer", accountId);
            await observer.Database.OpenConnectionAsync();
            await using var command = observer.Database.GetDbConnection().CreateCommand();
            command.CommandText = "select \"wait_event_type\" from pg_stat_activity where pid = @pid";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "pid";
            parameter.Value = backendPid;
            command.Parameters.Add(parameter);
            if (string.Equals(await command.ExecuteScalarAsync() as string, "Lock", StringComparison.Ordinal)) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("The sync import did not reach the expected PostgreSQL Cave lock wait.");
    }
}
