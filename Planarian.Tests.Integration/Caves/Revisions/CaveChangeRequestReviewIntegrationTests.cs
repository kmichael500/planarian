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

public sealed class CaveChangeRequestReviewIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ReviewerCannotApproveOrRejectAProposalVersionThatWasSupersededAfterLoading()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ReviewerCannotApproveOrRejectAProposalVersionThatWasSupersededAfterLoading));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string approvalRequestId;
        string rejectionRequestId;
        string approvalV1;
        string rejectionV1;
        string approvalV2;
        string rejectionV2;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            approvalRequestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Approval V1"), default);
            rejectionRequestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Rejection V1"), default);
            approvalV1 = await CurrentVersionAsync(contributor, approvalRequestId);
            rejectionV1 = await CurrentVersionAsync(contributor, rejectionRequestId);
            approvalV2 = await requests.AddVersionAsync(approvalRequestId, tenant.RevisionId, approvalV1,
                PublishableProposal(tenant, locationTag.Id, "Approval V2"), false, false, default);
            rejectionV2 = await requests.AddVersionAsync(rejectionRequestId, tenant.RevisionId, rejectionV1,
                PublishableProposal(tenant, locationTag.Id, "Rejection V2"), false, false, default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            var service = IntegrationTestServices.For(reviewer).CaveChangeRequests;
            var approvalConflict = await Assert.ThrowsAsync<CaveProposalVersionConflictException>(() =>
                service.ApproveAsync(approvalRequestId, approvalV1, null, default));
            Assert.Equal(approvalV2, approvalConflict.ActualProposalVersionId);
            var rejectionConflict = await Assert.ThrowsAsync<CaveProposalVersionConflictException>(() =>
                service.RejectAsync(rejectionRequestId, rejectionV1, null, default));
            Assert.Equal(rejectionV2, rejectionConflict.ActualProposalVersionId);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal("Cave A", (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(cave => cave.Id == tenant.CaveId)).Name);
        Assert.Single(await verify.CaveRevisions.Where(revision => revision.CaveId == tenant.CaveId).ToListAsync());
        var requestsAfter = await verify.CaveChangeRequests.Where(request =>
            request.Id == approvalRequestId || request.Id == rejectionRequestId).ToListAsync();
        Assert.All(requestsAfter, request => Assert.Equal(CaveChangeRequestStatus.Pending, request.Status));
        Assert.Contains(requestsAfter, request => request.Id == approvalRequestId &&
            request.CurrentProposalVersionId == approvalV2);
        Assert.Contains(requestsAfter, request => request.Id == rejectionRequestId &&
            request.CurrentProposalVersionId == rejectionV2);
    }

    [Fact]
    public async Task RejectionDoesNotPublishAndStaleBaseCannotBePreparedForApproval()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RejectionDoesNotPublishAndStaleBaseCannotBePreparedForApproval));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        string rejectedId;
        string staleId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var repository = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            rejectedId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Rejected"), default);
            staleId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Stale"), default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await new CaveChangeRequestRepository(reviewer, reviewer.RequestUser)
                .RejectAsync(rejectedId, await CurrentVersionAsync(reviewer, rejectedId),
                    "Not enough evidence", default);
        }

        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, cave => cave.Name = "Newer state");
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await using var transaction = await reviewer.Database.BeginTransactionAsync();
            var mutations = new CaveMutationRepository(reviewer, reviewer.RequestUser,
                new CavePublishedSnapshotRepository(reviewer, reviewer.RequestUser));
            await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
                mutations.PrepareExistingAsync(tenant.CaveId, tenant.RevisionId));
            await transaction.RollbackAsync();
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
        Assert.Equal(CaveChangeRequestStatus.Rejected,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == rejectedId)).Status);
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == staleId)).Status);
    }
}
