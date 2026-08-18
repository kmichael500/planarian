using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
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
    public async Task RequestKeyShareAllowsOrdinaryPublicationThenRequiresExplicitRereview()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RequestKeyShareAllowsOrdinaryPublicationThenRequiresExplicitRereview));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        await using var contributorDb = database.CreateDbContext("contributor", tenant.AccountId);
        var caveLockAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var createTask = new CaveChangeRequestRepository(contributorDb, contributorDb.RequestUser).CreateAsync(
            tenant.CaveId, tenant.RevisionId, async _ =>
            {
                caveLockAcquired.SetResult();
                await finishRequest.Task;
                return PublishableProposal(tenant, quality.Id, "Created against old base");
            }, default);
        await caveLockAcquired.Task;

        string newerRevisionId;
        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            newerRevisionId = (await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,
                cave => cave.Narrative = "Ordinary update was not blocked")).RevisionId!;
        }

        finishRequest.SetResult();
        var requestId = await createTask;
        var versionOne = await CurrentVersionAsync(contributorDb, requestId);
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
            await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
                reviewer.ChangeRequests.ApproveAsync(requestId, versionOne, null, default));

        string versionTwo;
        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var revised = PublishableValues(tenant, quality.Id, "Explicitly re-reviewed");
            revised.Narrative = "Ordinary update was not blocked";
            versionTwo = await contributor.ChangeRequests.AddVersionAsync(requestId, revised, true,
                newerRevisionId, versionOne, default);
        }
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
            Assert.Equal(CaveChangeRequestDecisionResult.Approved,
                (await reviewer.ChangeRequests.ApproveAsync(requestId, versionTwo, null, default)).Result);
    }

    [Fact]
    public async Task ConcurrentRejectionsOfTheSameRequestProduceExactlyOneDecision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ConcurrentRejectionsOfTheSameRequestProduceExactlyOneDecision));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string versionId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Reject once"), default);
            versionId = await CurrentVersionAsync(contributor, requestId);
        }

        await using var reviewerOne = database.CreateDbContext("reviewer", tenant.AccountId);
        await using var reviewerTwo = database.CreateDbContext("reviewer", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(reviewerOne, tenant.AccountId);
        await CavePermissions.AuthenticateAsync(reviewerTwo, tenant.AccountId);

        var rejectOne = IntegrationTestServices.For(reviewerOne).CaveChangeRequests
            .RejectAsync(requestId, versionId, "Reason one", default);
        var rejectTwo = IntegrationTestServices.For(reviewerTwo).CaveChangeRequests
            .RejectAsync(requestId, versionId, "Reason two", default);
        var outcomes = await Task.WhenAll(CaptureAsync(rejectOne), CaptureAsync(rejectTwo));

        Assert.Single(outcomes.Where(outcome => outcome.Decision?.Result == CaveChangeRequestDecisionResult.Rejected));
        var failure = Assert.IsType<ApiException>(Assert.Single(outcomes.Where(outcome => outcome.Error is not null)).Error);
        Assert.Equal(400, failure.StatusCode);
        Assert.Equal("This request has already been reviewed.", failure.Message);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Rejected, request.Status);
        Assert.NotNull(request.ReviewerNotes);
        Assert.Contains(request.ReviewerNotes, new[] { "Reason one", "Reason two" });
        Assert.Single(await verify.CaveRevisions.Where(revision => revision.CaveId == tenant.CaveId).ToListAsync());
    }

    [Fact]
    public async Task ConcurrentApprovalsMovingIntoOneCountyCommitDistinctAllocatedNumbers()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ConcurrentApprovalsMovingIntoOneCountyCommitDistinctAllocatedNumbers));
        var target = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var sourceCounty = new AccountCountyTestData(target.AccountId, target.StateId, target.StateName,
            target.StateAbbreviation, "countysrca", "Source County", "A02");
        await using (var seed = database.CreateDbContext("seed", target.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = sourceCounty.CountyId, AccountId = target.AccountId, StateId = target.StateId,
                Name = sourceCounty.CountyName, DisplayId = sourceCounty.CountyDisplayId
            });
            await seed.SaveChangesAsync();
        }
        var caveOne = await CaveTestDataFactory.PublishBaselineRevisionAsync(database,
            await CaveTestDataFactory.AddCaveAsync(database, sourceCounty, "moving0001", "Moving One", 1),
            "movingrev1");
        var caveTwo = await CaveTestDataFactory.PublishBaselineRevisionAsync(database,
            await CaveTestDataFactory.AddCaveAsync(database, sourceCounty, "moving0002", "Moving Two", 2),
            "movingrev2");
        var quality = await ReferenceTestData.AddTagAsync(database, target.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        foreach (var cave in new[] { caveOne, caveTwo })
        {
            await CavePermissions.GrantViewAsync(database, cave, "contributor");
            await CavePermissions.GrantManagerAsync(database, cave, "reviewer");
        }

        string requestOne;
        string requestTwo;
        string versionOne;
        string versionTwo;
        await using (var contributor = await CaveTestActor.CreateAsync(database, target.AccountId, "contributor"))
        {
            var first = PublishableValues(caveOne, quality.Id, "Moved One");
            first.StateId = target.StateId; first.CountyId = target.CountyId;
            first.IsCountyNumberManuallySet = false;
            var second = PublishableValues(caveTwo, quality.Id, "Moved Two");
            second.StateId = target.StateId; second.CountyId = target.CountyId;
            second.IsCountyNumberManuallySet = false;
            requestOne = await contributor.ChangeRequests.CreateAsync(caveOne.CaveId, first,
                caveOne.RevisionId, default);
            requestTwo = await contributor.ChangeRequests.CreateAsync(caveTwo.CaveId, second,
                caveTwo.RevisionId, default);
            versionOne = await CurrentVersionAsync(contributor.Db, requestOne);
            versionTwo = await CurrentVersionAsync(contributor.Db, requestTwo);
        }

        await using var blocker = database.CreateDbContext("allocator-blocker", target.AccountId);
        await using var blockerTransaction = await blocker.Database.BeginTransactionAsync();
        await new CountyReferenceLockRepository(blocker, blocker.RequestUser)
            .LockForMutationAsync([target.CountyId]);

        await using var reviewerOne = database.CreateDbContext("reviewer", target.AccountId);
        await using var reviewerTwo = database.CreateDbContext("reviewer", target.AccountId);
        await CavePermissions.AuthenticateAsync(reviewerOne, target.AccountId);
        await CavePermissions.AuthenticateAsync(reviewerTwo, target.AccountId);
        await reviewerOne.Database.OpenConnectionAsync();
        await reviewerTwo.Database.OpenConnectionAsync();
        var pidOne = await PostgresLockAssertions.BackendPidAsync(reviewerOne);
        var pidTwo = await PostgresLockAssertions.BackendPidAsync(reviewerTwo);
        var approveOne = IntegrationTestServices.For(reviewerOne).CaveChangeRequests
            .ApproveAsync(requestOne, versionOne, null, default);
        var approveTwo = IntegrationTestServices.For(reviewerTwo).CaveChangeRequests
            .ApproveAsync(requestTwo, versionTwo, null, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, target.AccountId, pidOne, approveOne);
        await PostgresLockAssertions.AssertBlockedAsync(database, target.AccountId, pidTwo, approveTwo);
        await blockerTransaction.CommitAsync();
        var decisions = await Task.WhenAll(approveOne, approveTwo);

        await using var verify = database.CreateDbContext("verify", target.AccountId);
        var caves = await verify.Caves.IgnoreQueryFilters().Where(cave =>
            cave.Id == caveOne.CaveId || cave.Id == caveTwo.CaveId).ToListAsync();
        Assert.Equal(2, caves.Select(cave => cave.CountyNumber).Distinct().Count());
        Assert.All(caves, cave => Assert.Equal(target.CountyId, cave.CountyId));
        var snapshots = await verify.CaveRevisions.Where(revision =>
                decisions.Select(decision => decision.PublishedRevisionId).Contains(revision.Id))
            .Select(revision => revision.SnapshotJson).ToListAsync();
        Assert.Equal(caves.Select(cave => cave.CountyNumber).Order().ToList(), snapshots
            .Select(json => CaveSnapshotJson.Deserialize(json, 1).CountyNumber).Order().ToList());
    }

    [Fact]
    public async Task ConcurrentManualCountyNumberApprovalsReturnOneControlledDomainFailure()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ConcurrentManualCountyNumberApprovalsReturnOneControlledDomainFailure));
        var target = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var sourceCounty = new AccountCountyTestData(target.AccountId, target.StateId, target.StateName,
            target.StateAbbreviation, "countysrca", "Source County", "A02");
        await using (var seed = database.CreateDbContext("seed", target.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = sourceCounty.CountyId, AccountId = target.AccountId, StateId = target.StateId,
                Name = sourceCounty.CountyName, DisplayId = sourceCounty.CountyDisplayId
            });
            await seed.SaveChangesAsync();
        }
        var caveOne = await CaveTestDataFactory.PublishBaselineRevisionAsync(database,
            await CaveTestDataFactory.AddCaveAsync(database, sourceCounty, "manual0001", "Manual One", 1),
            "manualrev1");
        var caveTwo = await CaveTestDataFactory.PublishBaselineRevisionAsync(database,
            await CaveTestDataFactory.AddCaveAsync(database, sourceCounty, "manual0002", "Manual Two", 2),
            "manualrev2");
        var quality = await ReferenceTestData.AddTagAsync(database, target.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        foreach (var cave in new[] { caveOne, caveTwo })
        {
            await CavePermissions.GrantViewAsync(database, cave, "contributor");
            await CavePermissions.GrantManagerAsync(database, cave, "reviewer");
        }

        string requestOne;
        string requestTwo;
        string versionOne;
        string versionTwo;
        await using (var contributor = await CaveTestActor.CreateAsync(database, target.AccountId, "contributor"))
        {
            var first = PublishableValues(caveOne, quality.Id, "Manual winner or loser");
            first.StateId = target.StateId;
            first.CountyId = target.CountyId;
            first.CountyNumber = 50;
            var second = PublishableValues(caveTwo, quality.Id, "Manual loser or winner");
            second.StateId = target.StateId;
            second.CountyId = target.CountyId;
            second.CountyNumber = 50;
            requestOne = await contributor.ChangeRequests.CreateAsync(caveOne.CaveId, first,
                caveOne.RevisionId, default);
            requestTwo = await contributor.ChangeRequests.CreateAsync(caveTwo.CaveId, second,
                caveTwo.RevisionId, default);
            versionOne = await CurrentVersionAsync(contributor.Db, requestOne);
            versionTwo = await CurrentVersionAsync(contributor.Db, requestTwo);
        }

        await using var blocker = database.CreateDbContext("manual-blocker", target.AccountId);
        await using var blockerTransaction = await blocker.Database.BeginTransactionAsync();
        await new CountyReferenceLockRepository(blocker, blocker.RequestUser)
            .LockForMutationAsync([target.CountyId]);

        await using var reviewerOne = database.CreateDbContext("reviewer", target.AccountId);
        await using var reviewerTwo = database.CreateDbContext("reviewer", target.AccountId);
        await CavePermissions.AuthenticateAsync(reviewerOne, target.AccountId);
        await CavePermissions.AuthenticateAsync(reviewerTwo, target.AccountId);
        await reviewerOne.Database.OpenConnectionAsync();
        await reviewerTwo.Database.OpenConnectionAsync();
        var pidOne = await PostgresLockAssertions.BackendPidAsync(reviewerOne);
        var pidTwo = await PostgresLockAssertions.BackendPidAsync(reviewerTwo);
        var approveOne = IntegrationTestServices.For(reviewerOne).CaveChangeRequests
            .ApproveAsync(requestOne, versionOne, null, default);
        var approveTwo = IntegrationTestServices.For(reviewerTwo).CaveChangeRequests
            .ApproveAsync(requestTwo, versionTwo, null, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, target.AccountId, pidOne, approveOne);
        await PostgresLockAssertions.AssertBlockedAsync(database, target.AccountId, pidTwo, approveTwo);
        await blockerTransaction.CommitAsync();
        var outcomes = await Task.WhenAll(CaptureAsync(approveOne), CaptureAsync(approveTwo));

        Assert.Single(outcomes.Where(outcome => outcome.Decision is not null));
        var failure = Assert.IsType<ApiException>(Assert.Single(outcomes.Where(outcome => outcome.Error is not null))
            .Error);
        Assert.Equal(400, failure.StatusCode);
        Assert.Equal("That county number is already in use for the selected county.", failure.Message);

        await using var verify = database.CreateDbContext("verify", target.AccountId);
        var caves = await verify.Caves.IgnoreQueryFilters().Where(cave =>
            cave.Id == caveOne.CaveId || cave.Id == caveTwo.CaveId).ToListAsync();
        Assert.Single(caves.Where(cave => cave.CountyId == target.CountyId && cave.CountyNumber == 50));
        Assert.Single(caves.Where(cave => cave.CountyId == sourceCounty.CountyId));
        var requests = await verify.CaveChangeRequests.Where(request =>
            request.Id == requestOne || request.Id == requestTwo).ToListAsync();
        var winningRequest = Assert.Single(requests.Where(request => request.Status == CaveChangeRequestStatus.Approved));
        var losingRequest = Assert.Single(requests.Where(request => request.Status == CaveChangeRequestStatus.Pending));
        Assert.Null(losingRequest.ApprovedRevisionId);
        Assert.Single(await verify.CaveRevisions.Where(revision =>
            revision.ChangeRequestId == winningRequest.Id).ToListAsync());
        Assert.Empty(await verify.CaveRevisions.Where(revision =>
            revision.ChangeRequestId == losingRequest.Id).ToListAsync());
    }

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
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", System.Text.Encoding.UTF8.GetBytes("racing approval bytes"));
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string expectedVersionId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var approvalValues = PublishableValues(tenant, locationTag.Id, "Approval loses race");
            // This test isolates the Cave revision race. A manual county number intentionally takes the
            // county allocator lock, which would make the competing publication wait on a separate resource.
            approvalValues.IsCountyNumberManuallySet = false;
            requestId = await IntegrationTestServices.For(contributor).CaveChangeRequests.CreateAsync(tenant.CaveId,
                approvalValues, tenant.RevisionId, default);
            await new CaveChangeRequestRepository(contributor, contributor.RequestUser).StageFileAsync(
                requestId, file.FileId, file.FileTypeId, "Still staged", false, default);
            expectedVersionId = await CurrentVersionAsync(contributor, requestId);
        }

        var pause = new PauseCaveUpdateInterceptor();
        await using var approving = database.CreateDbContext("reviewer", tenant.AccountId, pause);
        await CavePermissions.AuthenticateAsync(approving, tenant.AccountId);
        var approvalTask = IntegrationTestServices.For(approving, blobs).CaveChangeRequests.ApproveAsync(requestId,
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

    private static async Task<(CaveChangeRequestDecisionVm? Decision, Exception? Error)> CaptureAsync(
        Task<CaveChangeRequestDecisionVm> task)
    {
        try
        {
            return (await task, null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }
}
