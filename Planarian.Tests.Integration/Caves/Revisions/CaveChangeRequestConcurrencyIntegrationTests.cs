using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Import.Planning;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Concurrency;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveChangeRequestConcurrencyIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ProposalVersionChangeAtFinalApprovalLockRollsBackPublication()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalVersionChangeAtFinalApprovalLockRollsBackPublication));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var stagedFile = await FileTestDataFactory.AddFileAsync(database, tenant);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        string requestId;
        string versionOneId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Version one"), default);
            versionOneId = await CurrentVersionAsync(contributor, requestId);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await using var transaction = await reviewer.Database.BeginTransactionAsync();
            var mutations = new CaveMutationRepository(reviewer, reviewer.RequestUser,
                new CavePublishedSnapshotRepository(reviewer, reviewer.RequestUser));
            var preparation = await mutations.PrepareExistingAsync(tenant.CaveId, tenant.RevisionId);

            await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
                await new CaveChangeRequestRepository(contributor, contributor.RequestUser).StageFileAsync(
                    requestId, stagedFile.FileId, stagedFile.FileTypeId, "Concurrent staged file", false, default);

            (await reviewer.Caves.IgnoreQueryFilters().SingleAsync(cave => cave.Id == tenant.CaveId)).Name =
                "Should roll back";
            await reviewer.SaveChangesAsync();
            var mutation = await mutations.PublishPreparedAsync(preparation, CaveRevisionSource.UserSubmission,
                CaveRevisionOperation.Update, requestId);

            var requests = new CaveChangeRequestRepository(reviewer, reviewer.RequestUser);
            await Assert.ThrowsAsync<CaveProposalVersionConflictException>(() =>
                requests.MarkApprovedAsync(requestId, versionOneId, mutation, null, default));
            await transaction.RollbackAsync();
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal("Cave A", (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(cave => cave.Id == tenant.CaveId)).Name);
        Assert.Single(await verify.CaveRevisions.Where(revision => revision.CaveId == tenant.CaveId).ToListAsync());
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Pending, request.Status);
        Assert.NotEqual(versionOneId, request.CurrentProposalVersionId);
        Assert.True(await verify.CaveChangeRequestStagedFiles.AnyAsync(row => row.ChangeRequestId == requestId));
    }

    [Fact]
    public async Task RequestCreationLockWinsAndHardDeleteWaitsThenRefuses()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RequestCreationLockWinsAndHardDeleteWaitsThenRefuses));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        await using var contributor = database.CreateDbContext("contributor", tenant.AccountId);
        await using var manager = database.CreateDbContext("reviewer", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(manager, tenant.AccountId);
        await manager.Database.OpenConnectionAsync();
        var managerPid = await PostgresLockAssertions.BackendPidAsync(manager);
        var caveLockAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishCreating = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var createTask = new CaveChangeRequestRepository(contributor, contributor.RequestUser).CreateAsync(
            tenant.CaveId, tenant.RevisionId, async _ =>
            {
                caveLockAcquired.SetResult();
                await finishCreating.Task;
                return Proposal(tenant, "Pending wins");
            }, default);
        await caveLockAcquired.Task;

        var deleteTask = IntegrationTestServices.For(manager).Caves.DeleteCave(tenant.CaveId, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, tenant.AccountId, managerPid, deleteTask);

        finishCreating.SetResult();
        var requestId = await createTask;
        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => deleteTask);
        Assert.Contains("Pending proposed changes", failure.Message);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == tenant.CaveId));
        Assert.True(await verify.CaveChangeRequests.AnyAsync(request => request.Id == requestId &&
            request.Status == CaveChangeRequestStatus.Pending));
        await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
        Assert.Equal(requestId, (await IntegrationTestServices.For(contributor).CaveChangeRequests.GetAsync(requestId, default)).Request.Id);
    }

    [Fact]
    public async Task HardDeleteLockWinsAndRequestCreationWaitsThenReturnsNotFound()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(HardDeleteLockWinsAndRequestCreationWaitsThenReturnsNotFound));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        await using var manager = database.CreateDbContext("reviewer", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(manager, tenant.AccountId);
        await using var deleteTransaction = await manager.Database.BeginTransactionAsync();
        await new CaveRepository(manager, manager.RequestUser).LockForHardDeleteAsync(tenant.CaveId);

        await using var contributor = database.CreateDbContext("contributor", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
        await contributor.Database.OpenConnectionAsync();
        var contributorPid = await PostgresLockAssertions.BackendPidAsync(contributor);
        var createTask = IntegrationTestServices.For(contributor).CaveChangeRequests.CreateAsync(tenant.CaveId,
            PublishableValues(tenant, locationTag.Id, "Loses to delete"), tenant.RevisionId, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, tenant.AccountId, contributorPid, createTask);

        await IntegrationTestServices.For(manager).Caves.DeleteCave(tenant.CaveId, default, deleteTransaction);
        await deleteTransaction.CommitAsync();
        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => createTask);
        Assert.Contains("Cave", failure.Message);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.False(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == tenant.CaveId));
        Assert.False(await verify.CaveChangeRequests.IgnoreQueryFilters().AnyAsync(request =>
            request.AccountId == tenant.AccountId && request.CaveId == tenant.CaveId &&
            request.Status == CaveChangeRequestStatus.Pending));
    }

    [Fact]
    public async Task DirectEditRacingAfterApprovalLoadReturnsConflictAndRollsBackPublication()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectEditRacingAfterApprovalLoadReturnsConflictAndRollsBackPublication));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await FileTestDataFactory.AddFileAsync(database, tenant);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string expectedVersionId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            requestId = await IntegrationTestServices.For(contributor).CaveChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, locationTag.Id, "Approval loses race"), tenant.RevisionId, default);
            await new CaveChangeRequestRepository(contributor, contributor.RequestUser).StageFileAsync(
                requestId, file.FileId, file.FileTypeId, "Still staged", false, default);
            expectedVersionId = await CurrentVersionAsync(contributor, requestId);
        }

        var pause = new PauseCaveUpdateInterceptor();
        await using var approving = database.CreateDbContext("reviewer", tenant.AccountId, pause);
        await CavePermissions.AuthenticateAsync(approving, tenant.AccountId);
        var approvalTask = IntegrationTestServices.For(approving).CaveChangeRequests.ApproveAsync(requestId,
            expectedVersionId, null, default);
        await pause.CaveUpdateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));

        string managerRevisionId;
        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            managerRevisionId = (await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,
                cave => cave.Narrative = "Concurrent manager edit")).RevisionId!;
        }

        pause.ContinueUpdate.SetResult();
        var conflict = await Assert.ThrowsAsync<CaveRevisionConflictException>(() => approvalTask);
        Assert.Equal(tenant.RevisionId, conflict.ExpectedRevisionId);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Pending, request.Status);
        Assert.Null(request.ApprovedRevisionId);
        Assert.Equal(expectedVersionId, request.CurrentProposalVersionId);
        Assert.Equal(2, await verify.CaveProposalVersions.CountAsync(row => row.ChangeRequestId == requestId));
        Assert.True(await verify.CaveChangeRequestStagedFiles.AnyAsync(row => row.ChangeRequestId == requestId &&
            row.FileId == file.FileId));
        Assert.Null((await verify.Files.SingleAsync(row => row.Id == file.FileId)).CaveId);
        Assert.Equal(managerRevisionId, (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == tenant.CaveId)).CurrentRevisionId);
        Assert.Equal("Concurrent manager edit", (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == tenant.CaveId)).Narrative);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
        Assert.False(await verify.CaveRevisions.AnyAsync(row => row.CaveId == tenant.CaveId &&
            row.Source == CaveRevisionSource.UserSubmission));

        await using var retry = database.CreateDbContext("reviewer", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(retry, tenant.AccountId);
        var stale = await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
            IntegrationTestServices.For(retry).CaveChangeRequests.ApproveAsync(requestId, expectedVersionId, null, default));
        Assert.Equal(managerRevisionId, stale.ActualRevisionId);
    }
}
