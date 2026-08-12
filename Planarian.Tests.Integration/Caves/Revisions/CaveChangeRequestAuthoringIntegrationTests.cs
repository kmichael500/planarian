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

public sealed class CaveChangeRequestAuthoringIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task InitialSemanticNoOpIsRejectedWithoutPersistingWorkflowState()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InitialSemanticNoOpIsRejectedWithoutPersistingWorkflowState));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var context = await IntegrationTestServices.For(contributor).CaveChangeRequests
                .GetAuthoringContextAsync(tenant.CaveId, default);
            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                IntegrationTestServices.For(contributor).CaveChangeRequests.CreateAsync(tenant.CaveId,
                    ValuesFromCave(context.Cave), context.ExpectedBaseRevisionId, default));
            Assert.Contains("does not contain any changes", failure.Message);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Empty(await verify.CaveChangeRequests.ToListAsync());
        Assert.Empty(await verify.CaveProposalVersions.ToListAsync());
        Assert.Empty(await verify.CaveChangeRequestStagedFiles.ToListAsync());
        Assert.Single(await verify.CaveRevisions.Where(row => row.CaveId == tenant.CaveId).ToListAsync());
    }

    [Fact]
    public async Task RevisedSemanticNoOpIsRejectedWithoutSupersedingTheValidVersion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RevisedSemanticNoOpIsRejectedWithoutSupersedingTheValidVersion));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        await using var contributor = database.CreateDbContext("contributor", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
        var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
        var requestId = await service.CreateAsync(tenant.CaveId,
            PublishableValues(tenant, locationTag.Id, "Valid proposal"), tenant.RevisionId, default);
        var validVersionId = await CurrentVersionAsync(contributor, requestId);
        var context = await service.GetAuthoringContextAsync(tenant.CaveId, default);

        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            service.AddVersionAsync(requestId, ValuesFromCave(context.Cave), false,
                tenant.RevisionId, validVersionId, default));

        Assert.Contains("does not contain any changes", failure.Message);
        contributor.ChangeTracker.Clear();
        Assert.Equal(validVersionId, await CurrentVersionAsync(contributor, requestId));
        Assert.Single(await contributor.CaveProposalVersions.Where(row => row.ChangeRequestId == requestId)
            .ToListAsync());
    }

    [Fact]
    public async Task AuthoringContextForMissingCaveReturnsNotFound()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AuthoringContextForMissingCaveReturnsNotFound));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await using var contributor = database.CreateDbContext("contributor", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);

        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            IntegrationTestServices.For(contributor).CaveChangeRequests.GetAuthoringContextAsync("missing00a", default));

        Assert.Contains("Cave", failure.Message);
    }

    [Fact]
    public async Task InitialAuthoringRejectsCaveChangedBeforePreviewOrAfterSuccessfulPreview()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InitialAuthoringRejectsCaveChangedBeforePreviewOrAfterSuccessfulPreview));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        var values = PublishableValues(tenant, locationTag.Id, "Expected B");

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            await IntegrationTestServices.For(contributor).CaveChangeRequests.PreviewAsync(tenant.CaveId, values,
                tenant.RevisionId, default);
        }

        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, cave => cave.Narrative = "C");
        }

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
            await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
                service.PreviewAsync(tenant.CaveId, values, tenant.RevisionId, default));
            await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
                service.CreateAsync(tenant.CaveId, values, tenant.RevisionId, default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Empty(await verify.CaveChangeRequests.ToListAsync());
    }

    [Fact]
    public async Task StaleRereviewRejectsCaveChangedBetweenPreviewAndSave()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StaleRereviewRejectsCaveChangedBetweenPreviewAndSave));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        string requestId;
        string versionOneId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var repository = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "V1"), default);
            versionOneId = (await contributor.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
        }

        string revisionB;
        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            revisionB = (await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, cave => cave.Narrative = "B"))
                .RevisionId!;
        }

        var values = PublishableValues(tenant, locationTag.Id, "V2", "B");
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            await IntegrationTestServices.For(contributor).CaveChangeRequests.PreviewVersionAsync(requestId, values, true,
                revisionB, versionOneId, default);
        }

        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            await mutations.PublishExistingAsync(tenant.CaveId, revisionB, CaveRevisionSource.ManagerEdit,
                CaveRevisionOperation.Update, cave => cave.Narrative = "C");
        }

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
            await Assert.ThrowsAsync<CaveRevisionConflictException>(() => service.PreviewVersionAsync(requestId,
                values, true, revisionB, versionOneId, default));
            await Assert.ThrowsAsync<CaveRevisionConflictException>(() => service.AddVersionAsync(requestId,
                values, true, revisionB, versionOneId, default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Single(await verify.CaveProposalVersions.Where(row => row.ChangeRequestId == requestId).ToListAsync());
    }

    [Fact]
    public async Task ConcurrentProposalEditorCannotAppendFromSupersededVersion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ConcurrentProposalEditorCannotAppendFromSupersededVersion));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        string requestId;
        string versionOneId;
        await using (var seed = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var repository = new CaveChangeRequestRepository(seed, seed.RequestUser);
            requestId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "V1"), default);
            versionOneId = (await seed.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
        }

        string versionTwoId;
        await using (var actorA = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(actorA, tenant.AccountId);
            versionTwoId = await IntegrationTestServices.For(actorA).CaveChangeRequests.AddVersionAsync(requestId,
                PublishableValues(tenant, locationTag.Id, "A created V2"), false,
                tenant.RevisionId, versionOneId, default);
        }
        await using (var actorB = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(actorB, tenant.AccountId);
            await Assert.ThrowsAsync<CaveProposalVersionConflictException>(() =>
                IntegrationTestServices.For(actorB).CaveChangeRequests.AddVersionAsync(requestId,
                    PublishableValues(tenant, locationTag.Id, "B stale values"), false,
                    tenant.RevisionId, versionOneId, default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(2, await verify.CaveProposalVersions.CountAsync(row => row.ChangeRequestId == requestId));
        Assert.Equal(versionTwoId, (await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
            .CurrentProposalVersionId);
    }

    [Fact]
    public async Task StaleProposalCanBeExplicitlyRevisedAgainstCurrentAndApproved()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StaleProposalCanBeExplicitlyRevisedAgainstCurrentAndApproved));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string versionOneId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Contributor proposal v1"), default);
            versionOneId = (await contributor.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
        }

        string revisionB;
        await using (var manager = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            var published = await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,
                cave => cave.Narrative = "Unrelated manager change");
            revisionB = published.RevisionId!;
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            var expectedVersionId = await CurrentVersionAsync(reviewer, requestId);
            var conflict = await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
                IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
                    expectedVersionId, null, default));
            Assert.Equal(revisionB, conflict.ActualRevisionId);
        }

        string versionTwoId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            versionTwoId = await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(requestId,
                PublishableValues(tenant, locationTag.Id, "Contributor proposal v2", "Unrelated manager change"),
                againstCurrent: true, expectedBaseRevisionId: revisionB,
                expectedProposalVersionId: versionOneId, default);
            Assert.False((await IntegrationTestServices.For(contributor).CaveChangeRequests.GetAsync(requestId, default)).Request.IsStale);
        }

        await using (var beforeApproval = database.CreateDbContext("verify", tenant.AccountId))
        {
            var versions = await beforeApproval.CaveProposalVersions.Where(row => row.ChangeRequestId == requestId)
                .OrderBy(row => row.CreatedOn).ToListAsync();
            Assert.Equal([versionOneId, versionTwoId], versions.Select(version => version.Id));
            Assert.Equal(tenant.RevisionId, versions[0].BaseRevisionId);
            Assert.Equal(revisionB, versions[1].BaseRevisionId);
            Assert.Equal(versionOneId, versions[1].PreviousProposalVersionId);
            Assert.Equal(versionTwoId, (await beforeApproval.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId);
        }

        CaveChangeRequestDecisionVm approved;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            approved = await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), "Re-reviewed", default);
        }

        Assert.Equal(CaveChangeRequestDecisionResult.Approved, approved.Result);
        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(3, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
        var revisionC = await verify.CaveRevisions.SingleAsync(row => row.Id == approved.PublishedRevisionId);
        Assert.Equal(revisionB, revisionC.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.UserSubmission, revisionC.Source);
        Assert.Equal(requestId, revisionC.ChangeRequestId);
        Assert.Equal(revisionC.Id, (await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
            .ApprovedRevisionId);
    }

    [Fact]
    public async Task ProposalVersionsAreImmutableAndApprovalPublishesExactlyOneLinkedRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalVersionsAreImmutableAndApprovalPublishesExactlyOneLinkedRevision));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        string requestId;
        string firstVersionId;
        string secondVersionId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var repository = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "First proposal"), default);
            firstVersionId = (await contributor.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
            secondVersionId = await repository.AddVersionAsync(requestId, tenant.RevisionId, firstVersionId,
                Proposal(tenant, "Accepted proposal"),
                reviewer: false, againstCurrent: false, default);
        }

        string publishedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await using var transaction = await reviewer.Database.BeginTransactionAsync();
            var mutations = new CaveMutationRepository(reviewer, reviewer.RequestUser,
                new CavePublishedSnapshotRepository(reviewer, reviewer.RequestUser));
            var requests = new CaveChangeRequestRepository(reviewer, reviewer.RequestUser);
            var preparation = await mutations.PrepareExistingAsync(tenant.CaveId, tenant.RevisionId);
            (await reviewer.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId)).Name = "Accepted proposal";
            await reviewer.SaveChangesAsync();
            var result = await mutations.PublishPreparedAsync(preparation, CaveRevisionSource.UserSubmission,
                CaveRevisionOperation.Update, requestId);
            await requests.MarkApprovedAsync(requestId, secondVersionId, result, "Reviewed", default);
            publishedRevisionId = result.RevisionId!;
            await transaction.CommitAsync();
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        var versions = await verify.CaveProposalVersions.Where(row => row.ChangeRequestId == requestId)
            .OrderBy(row => row.CreatedOn).ToListAsync();
        Assert.Equal([firstVersionId, secondVersionId], versions.Select(row => row.Id));
        Assert.Null(versions[0].PreviousProposalVersionId);
        Assert.Equal(firstVersionId, versions[1].PreviousProposalVersionId);
        Assert.Equal(CaveChangeRequestStatus.Approved, request.Status);
        Assert.Equal(publishedRevisionId, request.ApprovedRevisionId);
        Assert.Equal("Reviewed", request.ReviewerNotes);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
        var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == publishedRevisionId);
        Assert.Equal(requestId, revision.ChangeRequestId);
        Assert.Equal(CaveRevisionSource.UserSubmission, revision.Source);
    }
}
