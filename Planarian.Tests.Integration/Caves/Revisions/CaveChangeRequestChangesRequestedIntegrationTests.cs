using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Import.Planning;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Concurrency;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveChangeRequestChangesRequestedIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task RequestChangesSupportsDraftsExplicitResubmissionAndImmutableHistory()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RequestChangesSupportsDraftsExplicitResubmissionAndImmutableHistory));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual328");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string versionOne;
        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, quality.Id, "Initial submission"), tenant.RevisionId, default);
            versionOne = await CurrentVersionAsync(contributor.Db, requestId);
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            var decision = await reviewer.ChangeRequests.RequestChangesAsync(requestId, versionOne,
                "  Please verify the survey notes.  ", default);
            Assert.Equal(Planarian.Modules.Caves.Models.CaveChangeRequestDecisionResult.ChangesRequested,
                decision.Result);

            var detail = await reviewer.ChangeRequests.GetAsync(requestId, default);
            Assert.Equal(CaveChangeRequestStatus.ChangesRequested, detail.Request.Status);
            Assert.False(detail.Request.CanReview);
            var requested = Assert.Single(detail.ReviewEvents);
            Assert.Equal(CaveChangeRequestReviewAction.ChangesRequested, requested.Action);
            Assert.Equal(versionOne, requested.ProposalVersionId);
            Assert.Equal("Please verify the survey notes.", requested.Message);

            var approval = await Assert.ThrowsAsync<ApiException>(() =>
                reviewer.ChangeRequests.ApproveAsync(requestId, versionOne, null, default));
            Assert.Equal(400, approval.StatusCode);

            var reviewerDraft = await Assert.ThrowsAsync<ApiException>(() =>
                reviewer.ChangeRequests.AddVersionAsync(requestId,
                    PublishableValues(tenant, quality.Id, "Reviewer must not author this"), false,
                    tenant.RevisionId, versionOne, default));
            Assert.Equal(403, reviewerDraft.StatusCode);
        }
        string versionTwo;
        string versionThree;
        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var premature = await Assert.ThrowsAsync<ApiException>(() =>
                contributor.ChangeRequests.ResubmitAsync(requestId, versionOne, default));
            Assert.Equal(400, premature.StatusCode);
            Assert.Contains("newer proposal version", premature.Message, StringComparison.OrdinalIgnoreCase);

            versionTwo = await contributor.ChangeRequests.AddVersionAsync(requestId,
                PublishableValues(tenant, quality.Id, "Draft two"), false,
                tenant.RevisionId, versionOne, default);
            var afterFirstDraft = await contributor.ChangeRequests.GetAsync(requestId, default);
            Assert.Equal(CaveChangeRequestStatus.ChangesRequested, afterFirstDraft.Request.Status);

            versionThree = await contributor.ChangeRequests.AddVersionAsync(requestId,
                PublishableValues(tenant, quality.Id, "Draft three"), false,
                tenant.RevisionId, versionTwo, default);
            var afterSecondDraft = await contributor.ChangeRequests.GetAsync(requestId, default);
            Assert.Equal(CaveChangeRequestStatus.ChangesRequested, afterSecondDraft.Request.Status);
            Assert.Equal(versionThree, afterSecondDraft.Request.CurrentProposalVersionId);
        }
        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var decision = await contributor.ChangeRequests.ResubmitAsync(requestId, versionThree, default);
            Assert.Equal(Planarian.Modules.Caves.Models.CaveChangeRequestDecisionResult.Resubmitted,
                decision.Result);

            var detail = await contributor.ChangeRequests.GetAsync(requestId, default);
            Assert.Equal(CaveChangeRequestStatus.Pending, detail.Request.Status);
            Assert.Equal(2, detail.ReviewEvents.Count);
            Assert.Contains(detail.ReviewEvents, reviewEvent =>
                reviewEvent.Action == CaveChangeRequestReviewAction.Resubmitted &&
                reviewEvent.ProposalVersionId == versionThree);
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            var decision = await reviewer.ChangeRequests.ApproveAsync(requestId, versionThree, null, default);
            Assert.Equal(Planarian.Modules.Caves.Models.CaveChangeRequestDecisionResult.Approved,
                decision.Result);
        }
        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Approved, request.Status);
        var events = await verify.CaveChangeRequestReviewEvents
            .Where(reviewEvent => reviewEvent.ChangeRequestId == requestId)
            .OrderBy(reviewEvent => reviewEvent.CreatedOn).ThenBy(reviewEvent => reviewEvent.Id)
            .ToListAsync();
        Assert.Equal(3, events.Count);
        Assert.Contains(events, reviewEvent => reviewEvent.Action == CaveChangeRequestReviewAction.ChangesRequested &&
                                               reviewEvent.ProposalVersionId == versionOne &&
                                               reviewEvent.Message == "Please verify the survey notes.");
        Assert.Contains(events, reviewEvent => reviewEvent.Action == CaveChangeRequestReviewAction.Resubmitted &&
                                               reviewEvent.ProposalVersionId == versionThree);
        Assert.Contains(events, reviewEvent => reviewEvent.Action == CaveChangeRequestReviewAction.Approved &&
                                               reviewEvent.ProposalVersionId == versionThree);
    }

    [Fact]
    public async Task ChangesRequestedLeavesReviewerQueueButRetainsOpenRequestProtections()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ChangesRequestedLeavesReviewerQueueButRetainsOpenRequestProtections));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        var staged = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "change328f");
        string requestId;
        string versionId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Awaiting author"), default);
            await requests.StageFileAsync(requestId, staged.FileId, staged.FileTypeId,
                "Author draft", reviewer: false, default);
            versionId = await CurrentVersionAsync(contributor, requestId);
            var file = await contributor.Files.SingleAsync(row => row.Id == staged.FileId);
            file.ExpiresOn = DateTime.UtcNow.AddDays(-1);
            await contributor.SaveChangesAsync();
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            await reviewer.ChangeRequests.RequestChangesAsync(requestId, versionId,
                "Please revise this before review continues.", default);
            Assert.DoesNotContain((await reviewer.ChangeRequests.ListForReviewAsync(default)),
                request => request.Id == requestId);
            Assert.Empty((await reviewer.ChangeRequests.ListForReviewAsync(1, 10, default,
                CaveChangeRequestStatus.ChangesRequested)).Results);
        }

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var expired = (await new FileRepository(contributor, contributor.RequestUser)
                .GetExpiredFiles()).ToList();
            Assert.DoesNotContain(expired, file => file.Id == staged.FileId);
        }

        await using (var manager = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(manager, tenant.AccountId);
            var failure = await Assert.ThrowsAsync<ApiException>(() =>
                IntegrationTestServices.For(manager).Caves.DeleteCave(tenant.CaveId, default));
            Assert.Contains("Open proposed changes", failure.Message);
        }

        await using (var importer = database.CreateDbContext("importer", tenant.AccountId))
        {
            var harness = new CaveImportTestHarness(importer, importer.RequestUser);
            var csv = ImportDryRunIntegrationTests.CaveHeader + "\n" +
                      "Replacement,Replacement County,REP,2,AA,,,,10,2,1,1,,,,,,,,false,,\n";
            var plan = await harness.PlanCsvAsync(csv, syncExisting: true);
            Assert.Contains(plan.Deletions, deletion => deletion.CaveId == tenant.CaveId);
            var failure = await Assert.ThrowsAsync<ImportPlanConcurrencyException>(() =>
                harness.ExecuteAsync(plan, "changes-requested-sync.csv"));
            Assert.Contains("open change requests", failure.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(CaveChangeRequestStatus.ChangesRequested,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId)).Status);
        Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.AccountId == tenant.AccountId && cave.Id == tenant.CaveId));
        Assert.True(await verify.CaveChangeRequestStagedFiles.AnyAsync(link =>
            link.ChangeRequestId == requestId && link.FileId == staged.FileId));
        Assert.True(await verify.Files.AnyAsync(file => file.Id == staged.FileId));
    }

    [Fact]
    public async Task ChangesRequestedCanRebaseAgainstCurrentThenResubmitAndRejectTerminally()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ChangesRequestedCanRebaseAgainstCurrentThenResubmitAndRejectTerminally));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual329");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string versionOne;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, quality.Id, "Needs author work"), tenant.RevisionId, default);
            versionOne = await CurrentVersionAsync(contributor.Db, requestId);
        }
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
            await reviewer.ChangeRequests.RequestChangesAsync(requestId, versionOne,
                "Reconcile this with the latest Cave record.", default);

        string currentRevisionId;
        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            currentRevisionId = (await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,
                cave => cave.Narrative = "Published while author is revising")).RevisionId!;
        }

        string versionTwo;
        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var stale = await contributor.ChangeRequests.GetAsync(requestId, default);
            Assert.True(stale.Request.IsStale);
            Assert.Equal(CaveChangeRequestStatus.ChangesRequested, stale.Request.Status);

            var context = await contributor.ChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
            Assert.Equal(currentRevisionId, context.ExpectedBaseRevisionId);
            var values = PublishableValues(tenant, quality.Id, "Author revision against current");
            values.Narrative = "Published while author is revising";
            versionTwo = await contributor.ChangeRequests.AddVersionAsync(requestId, values, true,
                currentRevisionId, versionOne, default);
        }

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var draft = await contributor.ChangeRequests.GetAsync(requestId, default);
            Assert.Equal(CaveChangeRequestStatus.ChangesRequested, draft.Request.Status);
            Assert.False(draft.Request.IsStale);
            Assert.Equal(versionTwo, draft.Request.CurrentProposalVersionId);
            await contributor.ChangeRequests.ResubmitAsync(requestId, versionTwo, default);
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            Assert.Contains((await reviewer.ChangeRequests.ListForReviewAsync(default)),
                request => request.Id == requestId);
            var decision = await reviewer.ChangeRequests.RejectAsync(requestId, versionTwo,
                "The revision still does not resolve the concern.", default);
            Assert.Equal(Planarian.Modules.Caves.Models.CaveChangeRequestDecisionResult.Rejected,
                decision.Result);
        }

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var terminal = await Assert.ThrowsAsync<ApiException>(() =>
                contributor.ChangeRequests.ResubmitAsync(requestId, versionTwo, default));
            Assert.Equal(400, terminal.StatusCode);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Rejected, request.Status);
        var events = await verify.CaveChangeRequestReviewEvents
            .Where(reviewEvent => reviewEvent.ChangeRequestId == requestId)
            .OrderBy(reviewEvent => reviewEvent.CreatedOn).ThenBy(reviewEvent => reviewEvent.Id)
            .ToListAsync();
        Assert.Equal([
            CaveChangeRequestReviewAction.ChangesRequested,
            CaveChangeRequestReviewAction.Resubmitted,
            CaveChangeRequestReviewAction.Rejected,
        ], events.Select(reviewEvent => reviewEvent.Action).ToArray());
        Assert.Equal(versionOne, events[0].ProposalVersionId);
        Assert.Equal(versionTwo, events[1].ProposalVersionId);
        Assert.Equal(versionTwo, events[2].ProposalVersionId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task RequestChangesRequiresANonWhitespaceMessage(string message)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(RequestChangesRequiresANonWhitespaceMessage)}_{message.Length}");
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string versionId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Message required"), default);
            versionId = await CurrentVersionAsync(contributor, requestId);
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            var failure = await Assert.ThrowsAsync<ApiException>(() =>
                reviewer.ChangeRequests.RequestChangesAsync(requestId, versionId, message, default));
            Assert.Equal(400, failure.StatusCode);
            Assert.Equal("A message is required when requesting changes.", failure.Message);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId)).Status);
        Assert.False(await verify.CaveChangeRequestReviewEvents.AnyAsync(reviewEvent =>
            reviewEvent.ChangeRequestId == requestId));
    }

    [Fact]
    public async Task RequestChangesAndRejectRaceProducesExactlyOneReviewTransition()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RequestChangesAndRejectRaceProducesExactlyOneReviewTransition));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string versionId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Concurrent review decision"), default);
            versionId = await CurrentVersionAsync(contributor, requestId);
        }

        var pause = new PauseChangeRequestUpdateInterceptor();
        await using var requestChangesDb = database.CreateDbContext("reviewer", tenant.AccountId, pause);
        await using var rejectDb = database.CreateDbContext("reviewer", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(requestChangesDb, tenant.AccountId);
        await CavePermissions.AuthenticateAsync(rejectDb, tenant.AccountId);
        await rejectDb.Database.OpenConnectionAsync();
        var rejectPid = await PostgresLockAssertions.BackendPidAsync(rejectDb);

        var requestChanges = IntegrationTestServices.For(requestChangesDb).CaveChangeRequests
            .RequestChangesAsync(requestId, versionId, "Please revise this.", default);
        await pause.ChangeRequestUpdateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var reject = IntegrationTestServices.For(rejectDb).CaveChangeRequests
            .RejectAsync(requestId, versionId, "Reject instead", default);
        try
        {
            await PostgresLockAssertions.AssertBlockedAsync(database, tenant.AccountId, rejectPid, reject);
        }
        finally
        {
            pause.ContinueUpdate.TrySetResult();
        }

        var requestChangesDecision = await requestChanges;
        Assert.Equal(CaveChangeRequestDecisionResult.ChangesRequested, requestChangesDecision.Result);
        var failure = await Assert.ThrowsAsync<ApiException>(() => reject);
        Assert.Equal(400, failure.StatusCode);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.ChangesRequested, request.Status);
        var reviewEvent = Assert.Single(await verify.CaveChangeRequestReviewEvents
            .Where(row => row.ChangeRequestId == requestId).ToListAsync());
        Assert.Equal(CaveChangeRequestReviewAction.ChangesRequested, reviewEvent.Action);
    }

    [Fact]
    public async Task RequestChangesAndApproveRaceDoesNotHalfPublish()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RequestChangesAndApproveRaceDoesNotHalfPublish));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual330");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string versionId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, quality.Id, "Approve race"), tenant.RevisionId, default);
            versionId = await CurrentVersionAsync(contributor.Db, requestId);
        }

        var pause = new PauseChangeRequestUpdateInterceptor();
        await using var requestChangesDb = database.CreateDbContext("reviewer", tenant.AccountId, pause);
        await using var approveDb = database.CreateDbContext("reviewer", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(requestChangesDb, tenant.AccountId);
        await CavePermissions.AuthenticateAsync(approveDb, tenant.AccountId);
        await approveDb.Database.OpenConnectionAsync();
        var approvePid = await PostgresLockAssertions.BackendPidAsync(approveDb);

        var requestChanges = IntegrationTestServices.For(requestChangesDb).CaveChangeRequests
            .RequestChangesAsync(requestId, versionId, "Please revise this instead.", default);
        await pause.ChangeRequestUpdateReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var approve = IntegrationTestServices.For(approveDb).CaveChangeRequests
            .ApproveAsync(requestId, versionId, null, default);
        try
        {
            await PostgresLockAssertions.AssertBlockedAsync(database, tenant.AccountId, approvePid, approve);
        }
        finally
        {
            pause.ContinueUpdate.TrySetResult();
        }

        var requestChangesDecision = await requestChanges;
        Assert.Equal(CaveChangeRequestDecisionResult.ChangesRequested, requestChangesDecision.Result);
        var failure = await Assert.ThrowsAsync<ApiException>(() => approve);
        Assert.Equal(400, failure.StatusCode);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.ChangesRequested, request.Status);
        Assert.Null(request.ApprovedRevisionId);
        var reviewEvent = Assert.Single(await verify.CaveChangeRequestReviewEvents
            .Where(row => row.ChangeRequestId == requestId).ToListAsync());
        Assert.Equal(CaveChangeRequestReviewAction.ChangesRequested, reviewEvent.Action);
        Assert.Single(await verify.CaveRevisions.Where(row => row.CaveId == tenant.CaveId).ToListAsync());
    }
}
