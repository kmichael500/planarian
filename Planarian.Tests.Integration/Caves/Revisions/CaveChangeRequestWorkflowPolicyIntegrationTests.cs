using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Revisions;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveChangeRequestWorkflowPolicyIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task PendingRequestCanBeRevisedBySubmitterOrManagerButNotAnotherViewer()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PendingRequestCanBeRevisedBySubmitterOrManagerButNotAnotherViewer));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual341");
        await CavePermissions.GrantViewAsync(database, tenant, "submitter");
        await CavePermissions.GrantViewAsync(database, tenant, "viewer");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string versionOne;
        string versionTwo;
        await using (var submitter = await CaveTestActor.CreateAsync(database, tenant.AccountId, "submitter"))
        {
            requestId = await submitter.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, quality.Id, "Initial proposal"), tenant.RevisionId, default);
            versionOne = await CurrentVersionAsync(submitter.Db, requestId);

            var detail = await submitter.ChangeRequests.GetAsync(requestId, default);
            Assert.True(detail.Request.CanEdit);
            Assert.False(detail.Request.CanReview);

            versionTwo = await submitter.ChangeRequests.AddVersionAsync(requestId,
                PublishableValues(tenant, quality.Id, "Submitter follow-up"), false,
                tenant.RevisionId, versionOne, default);
        }

        await using (var viewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "viewer"))
        {
            var denied = await Assert.ThrowsAsync<ApiException>(() => viewer.ChangeRequests.AddVersionAsync(
                requestId, PublishableValues(tenant, quality.Id, "Viewer edit"), false,
                tenant.RevisionId, versionTwo, default));
            Assert.Equal(404, denied.StatusCode);
            Assert.Contains("Change request", denied.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var verifyDenied = database.CreateDbContext("verify-denied", tenant.AccountId))
        {
            Assert.Equal(versionTwo, await CurrentVersionAsync(verifyDenied, requestId));
            Assert.Equal(2, await verifyDenied.CaveProposalVersions.CountAsync(
                version => version.ChangeRequestId == requestId));
            Assert.Empty(await verifyDenied.CaveChangeRequestReviewEvents
                .Where(reviewEvent => reviewEvent.ChangeRequestId == requestId).ToListAsync());
        }

        string versionThree;
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            var detail = await reviewer.ChangeRequests.GetAsync(requestId, default);
            Assert.False(detail.Request.CanEdit);
            Assert.True(detail.Request.CanReview);

            versionThree = await reviewer.ChangeRequests.AddVersionAsync(requestId,
                PublishableValues(tenant, quality.Id, "Manager-authored proposal"), false,
                tenant.RevisionId, versionTwo, default);

            var staleRequestChanges = await Assert.ThrowsAsync<CaveProposalVersionConflictException>(() =>
                reviewer.ChangeRequests.RequestChangesAsync(
                    requestId, versionTwo, "Feedback for a superseded draft.", default));
            Assert.Equal(versionThree, staleRequestChanges.ActualProposalVersionId);

            var approved = await reviewer.ChangeRequests.ApproveAsync(requestId, versionThree, null, default);
            Assert.Equal(CaveChangeRequestDecisionResult.Approved, approved.Result);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Approved, request.Status);
        Assert.Equal(versionThree, request.CurrentProposalVersionId);
        Assert.Equal("Manager-authored proposal", (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(cave => cave.AccountId == tenant.AccountId && cave.Id == tenant.CaveId)).Name);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(revision => revision.CaveId == tenant.CaveId));

        var reviewEvent = Assert.Single(await verify.CaveChangeRequestReviewEvents
            .Where(eventRow => eventRow.ChangeRequestId == requestId).ToListAsync());
        Assert.Equal(CaveChangeRequestReviewAction.Approved, reviewEvent.Action);
        Assert.Equal(versionThree, reviewEvent.ProposalVersionId);

        var versions = await verify.CaveProposalVersions
            .Where(version => version.ChangeRequestId == requestId)
            .ToDictionaryAsync(version => version.Id);
        Assert.Equal(3, versions.Count);
        Assert.Null(versions[versionOne].PreviousProposalVersionId);
        Assert.Equal(versionOne, versions[versionTwo].PreviousProposalVersionId);
        Assert.Equal(versionTwo, versions[versionThree].PreviousProposalVersionId);
    }

    [Fact]
    public async Task ManagerSubmitterCanReviewAndApproveOwnPendingRequest()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ManagerSubmitterCanReviewAndApproveOwnPendingRequest));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual346");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager-submitter");

        string requestId;
        string versionId;
        await using (var actor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager-submitter"))
        {
            requestId = await actor.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, quality.Id, "Self-reviewed manager proposal"), tenant.RevisionId, default);
            versionId = await CurrentVersionAsync(actor.Db, requestId);

            var detail = await actor.ChangeRequests.GetAsync(requestId, default);
            Assert.True(detail.Request.CanEdit);
            Assert.True(detail.Request.CanReview);

            var decision = await actor.ChangeRequests.ApproveAsync(requestId, versionId, null, default);
            Assert.Equal(CaveChangeRequestDecisionResult.Approved, decision.Result);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Approved, request.Status);
        Assert.Equal(request.CreatedByUserId, request.ReviewerUserId);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(revision => revision.CaveId == tenant.CaveId));

        var reviewEvent = Assert.Single(await verify.CaveChangeRequestReviewEvents
            .Where(eventRow => eventRow.ChangeRequestId == requestId).ToListAsync());
        Assert.Equal(request.CreatedByUserId, reviewEvent.ActorUserId);
        Assert.Equal(CaveChangeRequestReviewAction.Approved, reviewEvent.Action);
        Assert.Equal(versionId, reviewEvent.ProposalVersionId);
    }

    [Fact]
    public async Task SubmitterLosingCaveVisibilityWhileChangesAreRequestedCannotReviseOrResubmit()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(SubmitterLosingCaveVisibilityWhileChangesAreRequestedCannotReviseOrResubmit));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual342");
        await CavePermissions.GrantViewAsync(database, tenant, "submitter");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string versionOne;
        await using (var submitter = await CaveTestActor.CreateAsync(database, tenant.AccountId, "submitter"))
        {
            requestId = await submitter.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, quality.Id, "Needs feedback"), tenant.RevisionId, default);
            versionOne = await CurrentVersionAsync(submitter.Db, requestId);
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
            await reviewer.ChangeRequests.RequestChangesAsync(
                requestId, versionOne, "Please revise this proposal.", default);

        await CavePermissions.RevokeAllAsync(database, tenant, "submitter");

        await using (var revoked = await CaveTestActor.CreateAsync(database, tenant.AccountId, "submitter"))
        {
            await Assert.ThrowsAsync<ApiException>(() => revoked.ChangeRequests.GetAsync(requestId, default));
            await Assert.ThrowsAsync<ApiException>(() => revoked.ChangeRequests.AddVersionAsync(
                requestId, PublishableValues(tenant, quality.Id, "Invisible draft"), false,
                tenant.RevisionId, versionOne, default));
            await Assert.ThrowsAsync<ApiException>(() =>
                revoked.ChangeRequests.ResubmitAsync(requestId, versionOne, default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.ChangesRequested, request.Status);
        Assert.Equal(versionOne, request.CurrentProposalVersionId);
        Assert.Single(await verify.CaveProposalVersions
            .Where(version => version.ChangeRequestId == requestId).ToListAsync());
        var reviewEvent = Assert.Single(await verify.CaveChangeRequestReviewEvents
            .Where(reviewEvent => reviewEvent.ChangeRequestId == requestId).ToListAsync());
        Assert.Equal(CaveChangeRequestReviewAction.ChangesRequested, reviewEvent.Action);
        Assert.Single(await verify.CaveRevisions.Where(revision => revision.CaveId == tenant.CaveId).ToListAsync());
    }

    [Fact]
    public async Task MultipleFeedbackCyclesRequireANewerDraftEachTimeAndPreserveReviewLineage()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(MultipleFeedbackCyclesRequireANewerDraftEachTimeAndPreserveReviewLineage));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual343");
        await CavePermissions.GrantViewAsync(database, tenant, "submitter");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string versionOne;
        await using (var submitter = await CaveTestActor.CreateAsync(database, tenant.AccountId, "submitter"))
        {
            requestId = await submitter.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, quality.Id, "Proposal v1"), tenant.RevisionId, default);
            versionOne = await CurrentVersionAsync(submitter.Db, requestId);
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
            await reviewer.ChangeRequests.RequestChangesAsync(
                requestId, versionOne, "First round of feedback.", default);

        string versionTwo;
        await using (var submitter = await CaveTestActor.CreateAsync(database, tenant.AccountId, "submitter"))
        {
            versionTwo = await submitter.ChangeRequests.AddVersionAsync(requestId,
                PublishableValues(tenant, quality.Id, "Proposal v2"), false,
                tenant.RevisionId, versionOne, default);
            await submitter.ChangeRequests.ResubmitAsync(requestId, versionTwo, default);
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
            await reviewer.ChangeRequests.RequestChangesAsync(
                requestId, versionTwo, "Second round of feedback.", default);

        string versionThree;
        await using (var submitter = await CaveTestActor.CreateAsync(database, tenant.AccountId, "submitter"))
        {
            var premature = await Assert.ThrowsAsync<ApiException>(() =>
                submitter.ChangeRequests.ResubmitAsync(requestId, versionTwo, default));
            Assert.Equal(400, premature.StatusCode);
            Assert.Contains("newer proposal version", premature.Message, StringComparison.OrdinalIgnoreCase);

            versionThree = await submitter.ChangeRequests.AddVersionAsync(requestId,
                PublishableValues(tenant, quality.Id, "Proposal v3"), false,
                tenant.RevisionId, versionTwo, default);

            var staleResubmit = await Assert.ThrowsAsync<CaveProposalVersionConflictException>(() =>
                submitter.ChangeRequests.ResubmitAsync(requestId, versionTwo, default));
            Assert.Equal(versionThree, staleResubmit.ActualProposalVersionId);

            await submitter.ChangeRequests.ResubmitAsync(requestId, versionThree, default);
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            var approved = await reviewer.ChangeRequests.ApproveAsync(requestId, versionThree, null, default);
            Assert.Equal(CaveChangeRequestDecisionResult.Approved, approved.Result);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Approved, request.Status);
        Assert.Equal(versionThree, request.CurrentProposalVersionId);
        Assert.Equal(3, await verify.CaveProposalVersions.CountAsync(version =>
            version.ChangeRequestId == requestId));
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(revision => revision.CaveId == tenant.CaveId));
        Assert.Equal("Proposal v3", (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(cave => cave.AccountId == tenant.AccountId && cave.Id == tenant.CaveId)).Name);

        var events = await verify.CaveChangeRequestReviewEvents
            .Where(reviewEvent => reviewEvent.ChangeRequestId == requestId)
            .OrderBy(reviewEvent => reviewEvent.CreatedOn).ThenBy(reviewEvent => reviewEvent.Id)
            .ToListAsync();
        var expectedEvents = new (CaveChangeRequestReviewAction Action, string ProposalVersionId)[]
        {
            (CaveChangeRequestReviewAction.ChangesRequested, versionOne),
            (CaveChangeRequestReviewAction.Resubmitted, versionTwo),
            (CaveChangeRequestReviewAction.ChangesRequested, versionTwo),
            (CaveChangeRequestReviewAction.Resubmitted, versionThree),
            (CaveChangeRequestReviewAction.Approved, versionThree),
        };
        Assert.Equal(expectedEvents,
            events.Select(reviewEvent => (reviewEvent.Action, reviewEvent.ProposalVersionId)).ToArray());
    }

    [Theory]
    [InlineData(CaveChangeRequestStatus.Approved)]
    [InlineData(CaveChangeRequestStatus.Rejected)]
    public async Task TerminalRequestRejectsRevisionAndReviewMutationsWithoutSideEffects(
        CaveChangeRequestStatus terminalStatus)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(TerminalRequestRejectsRevisionAndReviewMutationsWithoutSideEffects)}_{terminalStatus}");
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual344");
        await CavePermissions.GrantViewAsync(database, tenant, "submitter");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string versionId;
        await using (var submitter = await CaveTestActor.CreateAsync(database, tenant.AccountId, "submitter"))
        {
            requestId = await submitter.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, quality.Id, $"Terminal {terminalStatus}"), tenant.RevisionId, default);
            versionId = await CurrentVersionAsync(submitter.Db, requestId);
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            if (terminalStatus == CaveChangeRequestStatus.Approved)
                await reviewer.ChangeRequests.ApproveAsync(requestId, versionId, null, default);
            else
                await reviewer.ChangeRequests.RejectAsync(requestId, versionId, "Terminal rejection.", default);
        }

        int proposalVersionCount;
        int reviewEventCount;
        int revisionCount;
        string? caveRevisionId;
        await using (var before = database.CreateDbContext("before", tenant.AccountId))
        {
            proposalVersionCount = await before.CaveProposalVersions.CountAsync(version =>
                version.ChangeRequestId == requestId);
            reviewEventCount = await before.CaveChangeRequestReviewEvents.CountAsync(reviewEvent =>
                reviewEvent.ChangeRequestId == requestId);
            revisionCount = await before.CaveRevisions.CountAsync(revision => revision.CaveId == tenant.CaveId);
            caveRevisionId = await before.Caves.IgnoreQueryFilters()
                .Where(cave => cave.AccountId == tenant.AccountId && cave.Id == tenant.CaveId)
                .Select(cave => cave.CurrentRevisionId).SingleAsync();
        }

        await using (var submitter = await CaveTestActor.CreateAsync(database, tenant.AccountId, "submitter"))
        {
            var detail = await submitter.ChangeRequests.GetAsync(requestId, default);
            Assert.False(detail.Request.CanEdit);
            Assert.False(detail.Request.CanReview);
            await AssertBadRequestAsync(async () =>
            {
                await submitter.ChangeRequests.PreviewVersionAsync(
                    requestId, PublishableValues(tenant, quality.Id, "Forbidden terminal preview"), false,
                    tenant.RevisionId, versionId, default);
            });
            await AssertBadRequestAsync(() => submitter.ChangeRequests.AddVersionAsync(
                requestId, PublishableValues(tenant, quality.Id, "Forbidden terminal edit"), false,
                tenant.RevisionId, versionId, default));
            await AssertBadRequestAsync(() =>
                submitter.ChangeRequests.ResubmitAsync(requestId, versionId, default));
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            var detail = await reviewer.ChangeRequests.GetAsync(requestId, default);
            Assert.False(detail.Request.CanEdit);
            Assert.False(detail.Request.CanReview);
            await AssertBadRequestAsync(() => reviewer.ChangeRequests.RequestChangesAsync(
                requestId, versionId, "Too late.", default));
            await AssertBadRequestAsync(() =>
                reviewer.ChangeRequests.ApproveAsync(requestId, versionId, null, default));
            await AssertBadRequestAsync(() =>
                reviewer.ChangeRequests.RejectAsync(requestId, versionId, "Too late.", default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(terminalStatus, request.Status);
        Assert.Equal(versionId, request.CurrentProposalVersionId);
        Assert.Equal(proposalVersionCount, await verify.CaveProposalVersions.CountAsync(version =>
            version.ChangeRequestId == requestId));
        Assert.Equal(reviewEventCount, await verify.CaveChangeRequestReviewEvents.CountAsync(reviewEvent =>
            reviewEvent.ChangeRequestId == requestId));
        Assert.Equal(revisionCount, await verify.CaveRevisions.CountAsync(revision => revision.CaveId == tenant.CaveId));
        Assert.Equal(caveRevisionId, await verify.Caves.IgnoreQueryFilters()
            .Where(cave => cave.AccountId == tenant.AccountId && cave.Id == tenant.CaveId)
            .Select(cave => cave.CurrentRevisionId).SingleAsync());
    }

    private static async Task AssertBadRequestAsync(Func<Task> action)
    {
        var failure = await Assert.ThrowsAsync<ApiException>(action);
        Assert.Equal(400, failure.StatusCode);
    }
}
