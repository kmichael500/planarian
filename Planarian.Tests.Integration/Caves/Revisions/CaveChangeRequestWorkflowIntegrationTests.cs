using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Tags.Repositories;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class CaveChangeRequestWorkflowIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task InitialSemanticNoOpIsRejectedWithoutPersistingWorkflowState()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InitialSemanticNoOpIsRejectedWithoutPersistingWorkflowState));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await GrantViewAsync(database, tenant, "contributor");

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(contributor, tenant.AccountId);
            var context = await CreateChangeRequestService(contributor)
                .GetAuthoringContextAsync(tenant.CaveId, default);
            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                CreateChangeRequestService(contributor).CreateAsync(tenant.CaveId,
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
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await GrantViewAsync(database, tenant, "contributor");

        await using var contributor = database.CreateDbContext("contributor", tenant.AccountId);
        await AuthenticateAsync(contributor, tenant.AccountId);
        var service = CreateChangeRequestService(contributor);
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
    public async Task FileOnlyProposalVersionCanBeApprovedEndToEnd()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(FileOnlyProposalVersionCanBeApprovedEndToEnd));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await TestDataBuilder.AddFileAsync(database, tenant);
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string fileOnlyVersionId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(contributor, tenant.AccountId);
            var service = CreateChangeRequestService(contributor);
            var context = await service.GetAuthoringContextAsync(tenant.CaveId, default);
            var initialValues = ValuesFromCave(context.Cave);
            initialValues.Name = "Temporary field change";
            requestId = await service.CreateAsync(tenant.CaveId, initialValues, tenant.RevisionId, default);
            var repository = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            await repository.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Only file change", false,
                default);
            var stagedVersionId = await CurrentVersionAsync(contributor, requestId);
            var fileOnlyValues = ValuesFromCave(context.Cave);
            fileOnlyValues.Files = [new EditFileMetadataVm
                { Id = file.FileId, FileTypeTagId = file.FileTypeId, DisplayName = "Only file change" }];
            fileOnlyVersionId = await service.AddVersionAsync(requestId, fileOnlyValues, false,
                tenant.RevisionId, stagedVersionId, default);
            var detail = await service.GetVersionAsync(requestId, fileOnlyVersionId, default);
            Assert.Equal([file.FileId], detail.Diff.AddedFiles);
            Assert.Empty(detail.Diff.Scalars);
        }

        CaveChangeRequestDecisionVm decision;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            decision = await CreateChangeRequestService(reviewer).ApproveAsync(requestId, fileOnlyVersionId,
                "File-only publication", default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        var accepted = await verify.CaveRevisions.SingleAsync(row => row.Id == decision.PublishedRevisionId);
        var previous = await verify.CaveRevisions.SingleAsync(row => row.Id == tenant.RevisionId);
        var diff = new CaveRevisionDiffService().Compare(
            CaveSnapshotJson.Deserialize(previous.SnapshotJson, previous.SnapshotSchemaVersion),
            CaveSnapshotJson.Deserialize(accepted.SnapshotJson, accepted.SnapshotSchemaVersion));
        Assert.Equal(tenant.CaveName, cave.Name);
        Assert.Equal(CaveChangeRequestStatus.Approved, request.Status);
        Assert.Equal(accepted.Id, request.ApprovedRevisionId);
        Assert.Equal(requestId, accepted.ChangeRequestId);
        Assert.Equal(CaveRevisionSource.UserSubmission, accepted.Source);
        Assert.Equal([file.FileId], diff.AddedFiles);
        Assert.Empty(diff.Scalars);
        Assert.Empty(diff.AddedTags);
        Assert.Empty(diff.RemovedTags);
        Assert.Empty(diff.AddedEntrances);
        Assert.Empty(diff.RemovedEntrances);
        Assert.Empty(diff.ChangedEntrances);
        Assert.Empty(diff.RemovedFiles);
        Assert.Empty(diff.ChangedFiles);
        Assert.Empty(diff.ReferenceMetadataChanges);
        Assert.Equal(tenant.CaveId, (await verify.Files.SingleAsync(row => row.Id == file.FileId)).CaveId);
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(row => row.ChangeRequestId == requestId));
    }

    [Fact]
    public async Task SameAccountGuessedCaveWithoutViewPermissionCannotReceiveProposal()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(SameAccountGuessedCaveWithoutViewPermissionCannotReceiveProposal));
        var visible = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var hiddenCave = await TestDataBuilder.AddCaveAsync(database, visible, "hidden000a", "Hidden Cave", 2);
        var hidden = await TestDataBuilder.PublishBaselineRevisionAsync(database, hiddenCave, "revisionha");
        await GrantViewAsync(database, visible, "contributor");

        await using (var contributor = database.CreateDbContext("contributor", visible.AccountId))
        {
            await AuthenticateAsync(contributor, visible.AccountId);
            var values = new AddCaveVm
            {
                Id = hidden.CaveId,
                Name = "Guessed hidden Cave",
                AlternateNames = [],
                StateId = hidden.StateId,
                CountyId = hidden.CountyId,
                CountyNumber = hidden.CountyNumber,
                IsCountyNumberManuallySet = true,
                Entrances = []
            };

            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                CreateChangeRequestService(contributor).CreateAsync(hidden.CaveId, values,
                    hidden.RevisionId, default));
            Assert.Contains("Cave", failure.Message);
        }

        await using var verify = database.CreateDbContext("verify", visible.AccountId);
        Assert.False(await verify.CaveChangeRequests.AnyAsync(row => row.CaveId == hidden.CaveId));
        Assert.False(await verify.CaveProposalVersions.AnyAsync(row => row.CaveId == hidden.CaveId));
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(staged =>
            verify.CaveChangeRequests.Any(request =>
                request.Id == staged.ChangeRequestId && request.CaveId == hidden.CaveId)));
    }

    [Fact]
    public async Task AuthoringContextForMissingCaveReturnsNotFound()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AuthoringContextForMissingCaveReturnsNotFound));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await GrantViewAsync(database, tenant, "contributor");
        await using var contributor = database.CreateDbContext("contributor", tenant.AccountId);
        await AuthenticateAsync(contributor, tenant.AccountId);

        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            CreateChangeRequestService(contributor).GetAuthoringContextAsync("missing00a", default));

        Assert.Contains("Cave", failure.Message);
    }

    [Fact]
    public async Task InitialAuthoringRejectsCaveChangedBeforePreviewOrAfterSuccessfulPreview()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InitialAuthoringRejectsCaveChangedBeforePreviewOrAfterSuccessfulPreview));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await GrantViewAsync(database, tenant, "contributor");
        var values = PublishableValues(tenant, locationTag.Id, "Expected B");

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(contributor, tenant.AccountId);
            await CreateChangeRequestService(contributor).PreviewAsync(tenant.CaveId, values,
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
            await AuthenticateAsync(contributor, tenant.AccountId);
            var service = CreateChangeRequestService(contributor);
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
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await GrantViewAsync(database, tenant, "contributor");
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
            await AuthenticateAsync(contributor, tenant.AccountId);
            await CreateChangeRequestService(contributor).PreviewVersionAsync(requestId, values, true,
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
            await AuthenticateAsync(contributor, tenant.AccountId);
            var service = CreateChangeRequestService(contributor);
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
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await GrantViewAsync(database, tenant, "contributor");
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
            await AuthenticateAsync(actorA, tenant.AccountId);
            versionTwoId = await CreateChangeRequestService(actorA).AddVersionAsync(requestId,
                PublishableValues(tenant, locationTag.Id, "A created V2"), false,
                tenant.RevisionId, versionOneId, default);
        }
        await using (var actorB = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(actorB, tenant.AccountId);
            await Assert.ThrowsAsync<CaveProposalVersionConflictException>(() =>
                CreateChangeRequestService(actorB).AddVersionAsync(requestId,
                    PublishableValues(tenant, locationTag.Id, "B stale values"), false,
                    tenant.RevisionId, versionOneId, default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(2, await verify.CaveProposalVersions.CountAsync(row => row.ChangeRequestId == requestId));
        Assert.Equal(versionTwoId, (await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
            .CurrentProposalVersionId);
    }

    [Fact]
    public async Task ReviewerCannotApproveOrRejectAProposalVersionThatWasSupersededAfterLoading()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ReviewerCannotApproveOrRejectAProposalVersionThatWasSupersededAfterLoading));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
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
            await AuthenticateAsync(reviewer, tenant.AccountId);
            var service = CreateChangeRequestService(reviewer);
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
    public async Task ProposalVersionChangeAtFinalApprovalLockRollsBackPublication()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalVersionChangeAtFinalApprovalLockRollsBackPublication));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var stagedFile = await TestDataBuilder.AddFileAsync(database, tenant);
        await GrantViewAsync(database, tenant, "contributor");
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
    public async Task HistoricalProposalVersionCanBeInspectedOnlyWithinItsReadableRequest()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(HistoricalProposalVersionCanBeInspectedOnlyWithinItsReadableRequest));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await GrantViewAsync(database, tenant, "contributor");
        string requestId;
        string otherRequestId;
        string versionOneId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Original contributor proposal"), default);
            otherRequestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Other request"), default);
            versionOneId = await CurrentVersionAsync(contributor, requestId);
            await requests.AddVersionAsync(requestId, tenant.RevisionId, versionOneId,
                Proposal(tenant, "Reviewer version"), false, false, default);
            await AuthenticateAsync(contributor, tenant.AccountId);
            var service = CreateChangeRequestService(contributor);
            var historical = await service.GetVersionAsync(requestId, versionOneId, default);
            Assert.Equal("Original contributor proposal", historical.Proposed.Name);
            Assert.Contains(historical.Diff.Scalars, change => change.Path == "Name");
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.GetVersionAsync(otherRequestId, versionOneId, default));
        }
    }

    [Fact]
    public async Task ApplicationApprovalPublishesNormalizedCaveAndExactlyOneLinkedRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApplicationApprovalPublishesNormalizedCaveAndExactlyOneLinkedRevision));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            requestId = await new CaveChangeRequestRepository(contributor, contributor.RequestUser).CreateAsync(
                tenant.CaveId, tenant.RevisionId, PublishableProposal(tenant, locationTag.Id, "Approved Cave"),
                default);
        }

        CaveChangeRequestDecisionVm result;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            result = await CreateChangeRequestService(reviewer).ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), "Looks correct", default);
        }

        Assert.Equal(CaveChangeRequestDecisionResult.Approved, result.Result);
        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Approved, request.Status);
        Assert.Equal(result.PublishedRevisionId, request.ApprovedRevisionId);
        Assert.Equal("reviewer", request.ReviewerUserId);
        Assert.Equal("Looks correct", request.ReviewerNotes);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
        var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == result.PublishedRevisionId);
        Assert.Equal(CaveRevisionSource.UserSubmission, revision.Source);
        Assert.Equal(requestId, revision.ChangeRequestId);
        var published = CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);
        Assert.Equal("Approved Cave", published.Name);
        Assert.Single(published.Entrances);
        var normalized = await new CavePublishedSnapshotRepository(verify, verify.RequestUser)
            .BuildAsync(tenant.CaveId);
        Assert.Equal(CaveSnapshotJson.Serialize(normalized), CaveSnapshotJson.Serialize(published));
        Assert.Equal("Approved Cave", (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == tenant.CaveId)).Name);
    }

    [Fact]
    public async Task EntranceOtherTagsSurviveProposalApprovalAndUnrelatedDirectEdit()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(EntranceOtherTagsSurviveProposalApprovalAndUnrelatedDirectEdit));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var otherTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.CaveOther, "Sensitive entrance value", "entrothera");
        const string entranceId = "entrance0a";
        await TestDataBuilder.AddEntranceAsync(database, tenant, entranceId,
            locationQualityTagId: locationTag.Id);
        await using (var seed = database.CreateDbContext("other-tag-seed", tenant.AccountId))
        {
            seed.EntranceOtherTag.Add(new EntranceOtherTag
                { EntranceId = entranceId, TagTypeId = otherTag.Id });
            await seed.SaveChangesAsync();
        }
        await using (var baseline = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutation = await new CaveMutationRepository(baseline, baseline.RequestUser,
                    new CavePublishedSnapshotRepository(baseline, baseline.RequestUser))
                .PublishExistingAsync(tenant.CaveId, tenant.RevisionId, CaveRevisionSource.ManagerEdit,
                    CaveRevisionOperation.Update, cave => cave.Narrative = "Baseline with entrance tags");
            tenant = tenant with { RevisionId = mutation.RevisionId! };
        }
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(contributor, tenant.AccountId);
            var read = await new CaveRepository(contributor, contributor.RequestUser).GetCave(tenant.CaveId);
            Assert.Equal([otherTag.Id], read!.Entrances.Single().EntranceOtherTagIds);
            var values = ValuesFromCave(read);
            values.Name = "Name-only proposal";
            var service = CreateChangeRequestService(contributor);
            var preview = await service.PreviewAsync(tenant.CaveId, values, tenant.RevisionId, default);
            Assert.DoesNotContain(preview.Diff.RemovedTags, tag =>
                tag.Role == SnapshotTagRole.EntranceOther && tag.TagTypeId == otherTag.Id);
            requestId = await service.CreateAsync(tenant.CaveId, values, tenant.RevisionId, default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            await CreateChangeRequestService(reviewer).ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), null, default);
        }

        await using (var manager = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(manager, tenant.AccountId);
            var read = await new CaveRepository(manager, manager.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(read!);
            values.Narrative = "Unrelated direct edit";
            await CreateCaveService(manager).AddCave(values, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.True(await verify.EntranceOtherTag.AnyAsync(tag =>
            tag.EntranceId == entranceId && tag.TagTypeId == otherTag.Id));
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.ChangeRequestId == requestId)).SnapshotJson, 1);
        Assert.Contains(accepted.Entrances.Single(entrance => entrance.Id == entranceId).Tags,
            tag => tag.Role == SnapshotTagRole.EntranceOther && tag.TagTypeId == otherTag.Id);
        var currentRevisionId = (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(cave => cave.Id == tenant.CaveId)).CurrentRevisionId;
        var current = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == currentRevisionId)).SnapshotJson, 1);
        Assert.Contains(current.Entrances.Single(entrance => entrance.Id == entranceId).Tags,
            tag => tag.Role == SnapshotTagRole.EntranceOther && tag.TagTypeId == otherTag.Id);
    }

    [Fact]
    public async Task PendingRequestBlocksHardDeleteUntilItIsResolved()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PendingRequestBlocksHardDeleteUntilItIsResolved));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string versionOneId;
        string versionTwoId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Pending V1"), default);
            versionOneId = await CurrentVersionAsync(contributor, requestId);
            versionTwoId = await requests.AddVersionAsync(requestId, tenant.RevisionId, versionOneId,
                Proposal(tenant, "Rejected V2"), reviewer: false, againstCurrent: false, default);
        }

        await using (var manager = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(manager, tenant.AccountId);
            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                CreateCaveService(manager).DeleteCave(tenant.CaveId, default));
            Assert.Contains("Pending proposed changes", failure.Message);
        }
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            await CreateChangeRequestService(reviewer).RejectAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), "Resolved before deletion", default);
        }
        await using (var manager = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(manager, tenant.AccountId);
            await CreateCaveService(manager).DeleteCave(tenant.CaveId, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.False(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == tenant.CaveId));
        Assert.True(await verify.CaveChangeRequests.AnyAsync(request => request.Id == requestId));
        Assert.Equal(2, await verify.CaveProposalVersions.CountAsync(version =>
            version.ChangeRequestId == requestId));

        await using var contributorHistory = database.CreateDbContext("contributor", tenant.AccountId);
        await AuthenticateAsync(contributorHistory, tenant.AccountId);
        var history = CreateChangeRequestService(contributorHistory);
        var listed = Assert.Single(await history.ListMineAsync(default), request => request.Id == requestId);
        Assert.False(listed.CaveExists);
        Assert.False(listed.IsStale);
        Assert.Equal("Cave A", listed.CaveName);
        var detail = await history.GetAsync(requestId, default);
        Assert.Equal(CaveChangeRequestStatus.Rejected, detail.Request.Status);
        Assert.Equal("Resolved before deletion", detail.Request.ReviewerNotes);
        Assert.False(string.IsNullOrWhiteSpace(detail.Request.ReviewerName));
        Assert.False(detail.Request.CaveExists);
        Assert.False(detail.Request.IsStale);
        Assert.Equal("Pending V1", (await history.GetVersionAsync(requestId, versionOneId, default)).Proposed.Name);
        Assert.Equal("Rejected V2", (await history.GetVersionAsync(requestId, versionTwoId, default)).Proposed.Name);

        await using var reviewQueue = database.CreateDbContext("reviewer", tenant.AccountId);
        await AuthenticateAsync(reviewQueue, tenant.AccountId);
        Assert.DoesNotContain(await CreateChangeRequestService(reviewQueue).ListForReviewAsync(default),
            request => request.Id == requestId);
    }

    [Fact]
    public async Task ApprovedRequestAndProposalHistoryRemainReadableAfterLaterHardDelete()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovedRequestAndProposalHistoryRemainReadableAfterLaterHardDelete));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string proposalVersionId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Approved historical Cave"), default);
            proposalVersionId = await CurrentVersionAsync(contributor, requestId);
        }

        string approvedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            var service = CreateChangeRequestService(reviewer);
            approvedRevisionId = (await service.ApproveAsync(requestId, proposalVersionId,
                "Approved for publication", default)).PublishedRevisionId!;
            await CreateCaveService(reviewer).DeleteCave(tenant.CaveId, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.False(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == tenant.CaveId));
        var storedRequest = await verify.CaveChangeRequests.SingleAsync(request => request.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Approved, storedRequest.Status);
        Assert.Equal(approvedRevisionId, storedRequest.ApprovedRevisionId);
        Assert.True(await verify.CaveRevisions.AnyAsync(revision => revision.Id == approvedRevisionId &&
            revision.ChangeRequestId == requestId));

        await using var contributorHistory = database.CreateDbContext("contributor", tenant.AccountId);
        await AuthenticateAsync(contributorHistory, tenant.AccountId);
        var history = CreateChangeRequestService(contributorHistory);
        var detail = await history.GetAsync(requestId, default);
        Assert.Equal(CaveChangeRequestStatus.Approved, detail.Request.Status);
        Assert.Equal(approvedRevisionId, detail.Request.ApprovedRevisionId);
        Assert.Equal("Approved for publication", detail.Request.ReviewerNotes);
        Assert.False(detail.Request.CaveExists);
        Assert.False(detail.Request.IsStale);
        var version = await history.GetVersionAsync(requestId, proposalVersionId, default);
        Assert.Equal("Approved historical Cave", version.Proposed.Name);
        Assert.Contains(detail.Versions, candidate => candidate.Id == proposalVersionId);
    }

    [Fact]
    public async Task RequestCreationLockWinsAndHardDeleteWaitsThenRefuses()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RequestCreationLockWinsAndHardDeleteWaitsThenRefuses));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");

        await using var contributor = database.CreateDbContext("contributor", tenant.AccountId);
        await using var manager = database.CreateDbContext("reviewer", tenant.AccountId);
        await AuthenticateAsync(manager, tenant.AccountId);
        await manager.Database.OpenConnectionAsync();
        var managerPid = await BackendPidAsync(manager);
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

        var deleteTask = CreateCaveService(manager).DeleteCave(tenant.CaveId, default);
        await AssertBlockedByDatabaseLockAsync(database, tenant.AccountId, managerPid, deleteTask);

        finishCreating.SetResult();
        var requestId = await createTask;
        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => deleteTask);
        Assert.Contains("Pending proposed changes", failure.Message);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == tenant.CaveId));
        Assert.True(await verify.CaveChangeRequests.AnyAsync(request => request.Id == requestId &&
            request.Status == CaveChangeRequestStatus.Pending));
        await AuthenticateAsync(contributor, tenant.AccountId);
        Assert.Equal(requestId, (await CreateChangeRequestService(contributor).GetAsync(requestId, default)).Request.Id);
    }

    [Fact]
    public async Task HardDeleteLockWinsAndRequestCreationWaitsThenReturnsNotFound()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(HardDeleteLockWinsAndRequestCreationWaitsThenReturnsNotFound));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");

        await using var manager = database.CreateDbContext("reviewer", tenant.AccountId);
        await AuthenticateAsync(manager, tenant.AccountId);
        await using var deleteTransaction = await manager.Database.BeginTransactionAsync();
        await new CaveRepository(manager, manager.RequestUser).LockForHardDeleteAsync(tenant.CaveId);

        await using var contributor = database.CreateDbContext("contributor", tenant.AccountId);
        await AuthenticateAsync(contributor, tenant.AccountId);
        await contributor.Database.OpenConnectionAsync();
        var contributorPid = await BackendPidAsync(contributor);
        var createTask = CreateChangeRequestService(contributor).CreateAsync(tenant.CaveId,
            PublishableValues(tenant, locationTag.Id, "Loses to delete"), tenant.RevisionId, default);
        await AssertBlockedByDatabaseLockAsync(database, tenant.AccountId, contributorPid, createTask);

        await CreateCaveService(manager).DeleteCave(tenant.CaveId, default, deleteTransaction);
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
    public async Task StaleProposalCanBeExplicitlyRevisedAgainstCurrentAndApproved()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StaleProposalCanBeExplicitlyRevisedAgainstCurrentAndApproved));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
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
            await AuthenticateAsync(reviewer, tenant.AccountId);
            var expectedVersionId = await CurrentVersionAsync(reviewer, requestId);
            var conflict = await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
                CreateChangeRequestService(reviewer).ApproveAsync(requestId,
                    expectedVersionId, null, default));
            Assert.Equal(revisionB, conflict.ActualRevisionId);
        }

        string versionTwoId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(contributor, tenant.AccountId);
            versionTwoId = await CreateChangeRequestService(contributor).AddVersionAsync(requestId,
                PublishableValues(tenant, locationTag.Id, "Contributor proposal v2", "Unrelated manager change"),
                againstCurrent: true, expectedBaseRevisionId: revisionB,
                expectedProposalVersionId: versionOneId, default);
            Assert.False((await CreateChangeRequestService(contributor).GetAsync(requestId, default)).Request.IsStale);
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
            await AuthenticateAsync(reviewer, tenant.AccountId);
            approved = await CreateChangeRequestService(reviewer).ApproveAsync(requestId,
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
    public async Task ApplicationApprovalPublishesStagedFileInsideSingleRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApplicationApprovalPublishesStagedFileInsideSingleRevision));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await TestDataBuilder.AddFileAsync(database, tenant);
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Proposal with staged file"), default);
            var staged = await contributor.Files.SingleAsync(row => row.Id == file.FileId);
            staged.ExpiresOn = DateTime.UtcNow.AddDays(-1);
            await contributor.SaveChangesAsync();
            await requests.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Survey attachment",
                reviewer: false, default);
            await AuthenticateAsync(contributor, tenant.AccountId);
            var stagedVersionId = (await contributor.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
            var values = PublishableValues(tenant, locationTag.Id, "Proposal with staged file");
            values.Files = [new EditFileMetadataVm
                { Id = file.FileId, FileTypeTagId = file.FileTypeId, DisplayName = "Survey attachment" }];
            await CreateChangeRequestService(contributor).AddVersionAsync(requestId,
                values, againstCurrent: false, expectedBaseRevisionId: tenant.RevisionId,
                expectedProposalVersionId: stagedVersionId, default);
        }

        await using (var pending = database.CreateDbContext("verify", tenant.AccountId))
        {
            Assert.Null((await pending.Files.SingleAsync(row => row.Id == file.FileId)).CaveId);
            Assert.True(await pending.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == file.FileId));
            var currentVersionId = (await pending.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
            var proposal = CaveProposalJson.Deserialize((await pending.CaveProposalVersions.SingleAsync(row =>
                row.Id == currentVersionId)).ProposalJson, 1);
            Assert.Contains(proposal.Files, candidate => candidate.FileId == file.FileId &&
                candidate.Disposition == ProposalFileDisposition.PublishStaged);
            var original = CaveSnapshotJson.Deserialize((await pending.CaveRevisions.SingleAsync(row =>
                row.Id == tenant.RevisionId)).SnapshotJson, 1);
            Assert.DoesNotContain(original.Files, candidate => candidate.Id == file.FileId);
        }

        CaveChangeRequestDecisionVm result;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            result = await CreateChangeRequestService(reviewer).ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), null, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var publishedFile = await verify.Files.SingleAsync(row => row.Id == file.FileId);
        Assert.Equal(tenant.CaveId, publishedFile.CaveId);
        Assert.Null(publishedFile.ExpiresOn);
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == file.FileId));
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
        var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == result.PublishedRevisionId);
        Assert.Contains(CaveSnapshotJson.Deserialize(revision.SnapshotJson, 1).Files,
            candidate => candidate.Id == file.FileId);
    }

    [Fact]
    public async Task GenericMetadataUpdateRejectsPendingStagedFileButAllowsItAfterRejection()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(GenericMetadataUpdateRejectsPendingStagedFileButAllowsItAfterRejection));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var file = await TestDataBuilder.AddFileAsync(database, tenant, fileId: "boundary0a");
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string activeVersionId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Pending staged metadata boundary"), default);
            await requests.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Original staged name",
                reviewer: false, default);
            activeVersionId = await CurrentVersionAsync(contributor, requestId);
        }

        await using (var manager = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(manager, tenant.AccountId);
            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                CreateFileService(manager).UpdateFilesMetadata([new EditFileMetadataVm
                {
                    Id = file.FileId,
                    FileTypeTagId = file.FileTypeId,
                    DisplayName = "Generic mutation must fail"
                }], default));
            Assert.Contains("This file is staged in a Cave change request", failure.Message);
        }

        await using (var pending = database.CreateDbContext("verify", tenant.AccountId))
        {
            var storedFile = await pending.Files.SingleAsync(candidate => candidate.Id == file.FileId);
            Assert.Equal(("seed-a.pdf", (string?)null, file.FileTypeId),
                (storedFile.FileName, storedFile.DisplayName, storedFile.FileTypeTagId));
            var request = await pending.CaveChangeRequests.SingleAsync(candidate => candidate.Id == requestId);
            Assert.Equal(activeVersionId, request.CurrentProposalVersionId);
            var versions = await pending.CaveProposalVersions.Where(version =>
                version.ChangeRequestId == requestId).ToListAsync();
            Assert.Equal(2, versions.Count);
            var activeVersion = Assert.Single(versions, version => version.Id == activeVersionId);
            Assert.Contains(CaveProposalJson.Deserialize(activeVersion.ProposalJson, activeVersion.SchemaVersion).Files,
                candidate => candidate.FileId == file.FileId &&
                             candidate.DisplayName == "Original staged name");
            Assert.Single(await pending.CaveRevisions.Where(revision => revision.CaveId == tenant.CaveId)
                .ToListAsync());
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await new CaveChangeRequestRepository(reviewer, reviewer.RequestUser)
                .RejectAsync(requestId, activeVersionId, "Rejected", default);
        }

        await using (var manager = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(manager, tenant.AccountId);
            await CreateFileService(manager).UpdateFilesMetadata([new EditFileMetadataVm
            {
                Id = file.FileId,
                FileTypeTagId = file.FileTypeId,
                DisplayName = "Editable after rejection"
            }], default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(link => link.FileId == file.FileId));
        var editable = await verify.Files.SingleAsync(candidate => candidate.Id == file.FileId);
        Assert.Null(editable.CaveId);
        Assert.Equal("Editable after rejection", editable.DisplayName);
        Assert.Equal("Editable after rejection.pdf", editable.FileName);
        Assert.Single(await verify.CaveRevisions.Where(revision => revision.CaveId == tenant.CaveId).ToListAsync());
    }

    [Fact]
    public async Task ApprovalAtomicallyTransitionsStagedMetadataToPublishedRevisionBoundary()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalAtomicallyTransitionsStagedMetadataToPublishedRevisionBoundary));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await TestDataBuilder.AddFileAsync(database, tenant, fileId: "approve00a");
        var mapType = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Map", "filemap00a");
        var laterType = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Photograph", "filephotoa");
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string proposalVersionId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Staged publication boundary"), default);
            await requests.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Initial attachment",
                reviewer: false, default);
            var stagedVersionId = await CurrentVersionAsync(contributor, requestId);
            var values = PublishableValues(tenant, locationTag.Id, "Staged publication boundary");
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId,
                FileTypeTagId = mapType.Id,
                DisplayName = "Proposed survey map"
            }];
            await AuthenticateAsync(contributor, tenant.AccountId);
            proposalVersionId = await CreateChangeRequestService(contributor).AddVersionAsync(requestId, values,
                againstCurrent: false, tenant.RevisionId, stagedVersionId, default);
        }

        await using (var inspect = database.CreateDbContext("verify", tenant.AccountId))
        {
            var liveFile = await inspect.Files.SingleAsync(candidate => candidate.Id == file.FileId);
            Assert.Equal(("seed-a.pdf", (string?)null, file.FileTypeId),
                (liveFile.FileName, liveFile.DisplayName, liveFile.FileTypeTagId));
            var proposalRow = await inspect.CaveProposalVersions.SingleAsync(version =>
                version.Id == proposalVersionId);
            var proposed = Assert.Single(CaveProposalJson.Deserialize(proposalRow.ProposalJson,
                proposalRow.SchemaVersion).Files, candidate => candidate.FileId == file.FileId);
            Assert.Equal(("Proposed survey map.pdf", "Proposed survey map", mapType.Id),
                (proposed.FileName, proposed.DisplayName, proposed.FileTypeTagId));
        }

        // Reproduce approval's uncommitted association-delete + Cave attachment.
        // READ COMMITTED readers must still see the committed staging link and
        // reject the generic mutation until the whole transition commits.
        await using (var approval = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await using var approvalTransaction = await approval.Database.BeginTransactionAsync();
            var caves = new CaveRepository(approval, approval.RequestUser);
            Assert.Single(await caves.AttachStagedFilesAsync(requestId, tenant.CaveId, [file.FileId]));
            await approval.SaveChangesAsync();

            await using var concurrentManager = database.CreateDbContext("reviewer", tenant.AccountId);
            await AuthenticateAsync(concurrentManager, tenant.AccountId);
            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                CreateFileService(concurrentManager).UpdateFilesMetadata([new EditFileMetadataVm
                {
                    Id = file.FileId,
                    FileTypeTagId = laterType.Id,
                    DisplayName = "Cannot cross uncommitted approval"
                }], default));
            Assert.Contains("This file is staged in a Cave change request", failure.Message);
            await approvalTransaction.RollbackAsync();
        }

        string acceptedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            acceptedRevisionId = (await CreateChangeRequestService(reviewer).ApproveAsync(requestId,
                proposalVersionId, null, default)).PublishedRevisionId!;
        }

        string acceptedSnapshotJson;
        await using (var acceptedState = database.CreateDbContext("verify", tenant.AccountId))
        {
            Assert.False(await acceptedState.CaveChangeRequestStagedFiles.AnyAsync(link =>
                link.FileId == file.FileId));
            var publishedFile = await acceptedState.Files.SingleAsync(candidate => candidate.Id == file.FileId);
            Assert.Equal(tenant.CaveId, publishedFile.CaveId);
            Assert.Null(publishedFile.ExpiresOn);
            Assert.Equal(("Proposed survey map.pdf", "Proposed survey map", mapType.Id),
                (publishedFile.FileName, publishedFile.DisplayName, publishedFile.FileTypeTagId));
            var accepted = await acceptedState.CaveRevisions.SingleAsync(revision =>
                revision.Id == acceptedRevisionId);
            Assert.Equal(CaveRevisionSource.UserSubmission, accepted.Source);
            acceptedSnapshotJson = accepted.SnapshotJson;
        }

        await using (var manager = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(manager, tenant.AccountId);
            await CreateFileService(manager).UpdateFilesMetadata([new EditFileMetadataVm
            {
                Id = file.FileId,
                FileTypeTagId = laterType.Id,
                DisplayName = "Manager follow-up photograph"
            }], default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == tenant.CaveId);
        Assert.NotEqual(acceptedRevisionId, cave.CurrentRevisionId);
        var acceptedRevision = await verify.CaveRevisions.SingleAsync(revision => revision.Id == acceptedRevisionId);
        Assert.Equal(acceptedSnapshotJson, acceptedRevision.SnapshotJson);
        var acceptedFile = Assert.Single(CaveSnapshotJson.Deserialize(acceptedRevision.SnapshotJson,
            acceptedRevision.SnapshotSchemaVersion).Files, candidate => candidate.Id == file.FileId);
        Assert.Equal(("Proposed survey map.pdf", "Proposed survey map", mapType.Id),
            (acceptedFile.FileName, acceptedFile.DisplayName, acceptedFile.FileTypeTagId));
        var managerRevision = await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == cave.CurrentRevisionId);
        Assert.Equal(CaveRevisionSource.ManagerEdit, managerRevision.Source);
        Assert.Equal(acceptedRevisionId, managerRevision.PreviousRevisionId);
        var managerFile = Assert.Single(CaveSnapshotJson.Deserialize(managerRevision.SnapshotJson,
            managerRevision.SnapshotSchemaVersion).Files, candidate => candidate.Id == file.FileId);
        Assert.Equal(("Manager follow-up photograph.pdf", "Manager follow-up photograph", laterType.Id),
            (managerFile.FileName, managerFile.DisplayName, managerFile.FileTypeTagId));
    }

    [Fact]
    public async Task PublishedFileTypeAndDisplayNamePreviewMatchApprovedRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PublishedFileTypeAndDisplayNamePreviewMatchApprovedRevision));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await TestDataBuilder.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "publish00a");
        var mapType = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Map", "filemap00a");
        await using (var seed = database.CreateDbContext("published-file-seed", tenant.AccountId))
        {
            (await seed.TagTypes.SingleAsync(tag => tag.Id == file.FileTypeId)).Name = "Report";
            var entity = await seed.Files.SingleAsync(row => row.Id == file.FileId);
            entity.FileName = "survey.pdf";
            entity.DisplayName = "Survey";
            await seed.SaveChangesAsync();
            var mutations = new CaveMutationRepository(seed, seed.RequestUser,
                new CavePublishedSnapshotRepository(seed, seed.RequestUser));
            tenant = tenant with { RevisionId = (await mutations.PublishExistingAsync(tenant.CaveId,
                tenant.RevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, _ => { }))
                .RevisionId! };
        }
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(contributor, tenant.AccountId);
            var cave = await new CaveRepository(contributor, contributor.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            values.Entrances = PublishableValues(tenant, locationTag.Id, cave!.Name).Entrances;
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId, FileTypeTagId = mapType.Id, DisplayName = "Survey Map"
            }];
            var service = CreateChangeRequestService(contributor);
            var preview = await service.PreviewAsync(tenant.CaveId, values, tenant.RevisionId, default);
            var before = Assert.Single(preview.Base.Files);
            var proposed = Assert.Single(preview.Proposed.Files);
            Assert.Equal(file.FileTypeId, before.FileTypeTagId);
            Assert.Equal("Report", before.FileTypeNameAtRevision);
            Assert.Equal(mapType.Id, proposed.FileTypeTagId);
            Assert.Equal("Map", proposed.FileTypeNameAtRevision);
            Assert.Equal("Survey Map.pdf", proposed.FileName);
            Assert.Contains(file.FileId, preview.Diff.ChangedFiles);
            requestId = await service.CreateAsync(tenant.CaveId, values, tenant.RevisionId, default);
        }

        string approvedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            var detail = await CreateChangeRequestService(reviewer).GetAsync(requestId, default);
            var reviewed = Assert.Single(detail.Proposed.Files);
            Assert.Equal((mapType.Id, "Map", "Survey Map.pdf"),
                (reviewed.FileTypeTagId, reviewed.FileTypeNameAtRevision, reviewed.FileName));
            approvedRevisionId = (await CreateChangeRequestService(reviewer).ApproveAsync(requestId,
                detail.Request.CurrentProposalVersionId, null, default)).PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == approvedRevisionId)).SnapshotJson, 1);
        var acceptedFile = Assert.Single(accepted.Files);
        Assert.Equal((mapType.Id, "Map", "Survey Map.pdf"),
            (acceptedFile.FileTypeTagId, acceptedFile.FileTypeNameAtRevision, acceptedFile.FileName));
    }

    [Fact]
    public async Task StagedFileSelectedTypeAndFilenameAreImmutableAndMatchApproval()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StagedFileSelectedTypeAndFilenameAreImmutableAndMatchApproval));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await TestDataBuilder.AddFileAsync(database, tenant, fileId: "stgtype00a");
        var mapType = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Map", "filemap00a");
        await using (var seed = database.CreateDbContext("staged-file-type-seed", tenant.AccountId))
        {
            (await seed.TagTypes.SingleAsync(tag => tag.Id == file.FileTypeId)).Name = "Report";
            (await seed.Files.SingleAsync(row => row.Id == file.FileId)).FileName = "survey.pdf";
            await seed.SaveChangesAsync();
        }
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string versionId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Staged map"), default);
            await requests.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Survey", false, default);
            var stagedVersionId = await CurrentVersionAsync(contributor, requestId);
            var values = PublishableValues(tenant, locationTag.Id, "Staged map");
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId, FileTypeTagId = mapType.Id, DisplayName = "Survey Map"
            }];
            await AuthenticateAsync(contributor, tenant.AccountId);
            versionId = await CreateChangeRequestService(contributor).AddVersionAsync(requestId, values,
                againstCurrent: false, expectedBaseRevisionId: tenant.RevisionId,
                expectedProposalVersionId: stagedVersionId, default);
        }

        await using (var inspect = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(inspect, tenant.AccountId);
            var stored = await inspect.CaveProposalVersions.SingleAsync(version => version.Id == versionId);
            var intent = Assert.Single(CaveProposalJson.Deserialize(stored.ProposalJson, stored.SchemaVersion).Files,
                candidate => candidate.FileId == file.FileId);
            Assert.Equal((mapType.Id, "Map", "Survey Map.pdf"),
                (intent.FileTypeTagId, intent.FileTypeName, intent.FileName));
            var historical = await CreateChangeRequestService(inspect).GetVersionAsync(requestId, versionId, default);
            var presented = Assert.Single(historical.Proposed.Files);
            Assert.Equal((mapType.Id, "Map", "Survey Map.pdf"),
                (presented.FileTypeTagId, presented.FileTypeNameAtRevision, presented.FileName));
        }

        string approvedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            approvedRevisionId = (await CreateChangeRequestService(reviewer).ApproveAsync(requestId, versionId,
                null, default)).PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == approvedRevisionId)).SnapshotJson, 1);
        var acceptedFile = Assert.Single(accepted.Files);
        Assert.Equal((mapType.Id, "Map", "Survey Map.pdf"),
            (acceptedFile.FileTypeTagId, acceptedFile.FileTypeNameAtRevision, acceptedFile.FileName));
    }

    [Fact]
    public async Task HistoricalStagedFilePresentationDoesNotDriftWhileLiveFileExists()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(HistoricalStagedFilePresentationDoesNotDriftWhileLiveFileExists));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var file = await TestDataBuilder.AddFileAsync(database, tenant, fileId: "drift0000a");
        var mapType = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Map", "filemap00a");
        await GrantViewAsync(database, tenant, "contributor");
        string requestId;
        string versionId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            (await contributor.TagTypes.SingleAsync(tag => tag.Id == file.FileTypeId)).Name = "Report";
            (await contributor.Files.SingleAsync(row => row.Id == file.FileId)).FileName = "survey.pdf";
            await contributor.SaveChangesAsync();
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Immutable staged metadata"), default);
            await requests.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Version One", false, default);
            versionId = await CurrentVersionAsync(contributor, requestId);
        }

        await using (var mutate = database.CreateDbContext("live-file-mutation", tenant.AccountId))
        {
            var live = await mutate.Files.SingleAsync(row => row.Id == file.FileId);
            live.FileTypeTagId = mapType.Id;
            live.FileName = "today.png";
            live.DisplayName = "Today";
            await mutate.SaveChangesAsync();
        }

        await using var audit = database.CreateDbContext("contributor", tenant.AccountId);
        await AuthenticateAsync(audit, tenant.AccountId);
        var historical = await CreateChangeRequestService(audit).GetVersionAsync(requestId, versionId, default);
        var presented = Assert.Single(historical.Proposed.Files);
        Assert.Equal((file.FileTypeId, "Report", "Version One.pdf", "Version One"),
            (presented.FileTypeTagId, presented.FileTypeNameAtRevision, presented.FileName,
                presented.DisplayName));
        Assert.Empty(historical.UnavailableStagedFileIds);
    }

    [Fact]
    public async Task PendingStagedFileIsExcludedFromExpirationUntilRequestIsRejected()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PendingStagedFileIsExcludedFromExpirationUntilRequestIsRejected));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        var stagedFile = await TestDataBuilder.AddFileAsync(database, tenant, fileId: "staged000a");
        const string unrelatedFileId = "expired00a";
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Pending files"), default);
            contributor.Files.Add(new Planarian.Model.Database.Entities.RidgeWalker.File
            {
                Id = unrelatedFileId,
                AccountId = tenant.AccountId,
                FileTypeTagId = stagedFile.FileTypeId,
                FileName = "expired.pdf",
                BlobKey = "expired",
                BlobContainer = "test"
            });
            await contributor.SaveChangesAsync();
            var files = await contributor.Files.Where(row => row.Id == stagedFile.FileId ||
                row.Id == unrelatedFileId).ToListAsync();
            foreach (var file in files) file.ExpiresOn = DateTime.UtcNow.AddDays(-1);
            await contributor.SaveChangesAsync();
            await requests.StageFileAsync(requestId, stagedFile.FileId, stagedFile.FileTypeId, "Staged",
                reviewer: false, default);

            var expired = (await new FileRepository(contributor, contributor.RequestUser).GetExpiredFiles()).ToList();
            Assert.DoesNotContain(expired, file => file.Id == stagedFile.FileId);
            Assert.Contains(expired, file => file.Id == unrelatedFileId);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
            await new CaveChangeRequestRepository(reviewer, reviewer.RequestUser)
                .RejectAsync(requestId, await CurrentVersionAsync(reviewer, requestId), "Rejected", default);

        await using var afterRejection = database.CreateDbContext("verify", tenant.AccountId);
        var nowExpired = (await new FileRepository(afterRejection, afterRejection.RequestUser)
            .GetExpiredFiles()).ToList();
        Assert.Contains(nowExpired, file => file.Id == stagedFile.FileId);
    }

    [Fact]
    public async Task HistoricalVersionPreservesUnavailableStagedAttachmentIntent()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(HistoricalVersionPreservesUnavailableStagedAttachmentIntent));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var stagedFile = await TestDataBuilder.AddFileAsync(database, tenant, fileId: "history00a");
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string versionWithFileId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Historical file"), default);
            var file = await contributor.Files.SingleAsync(row => row.Id == stagedFile.FileId);
            file.DisplayName = "Lost survey attachment";
            file.ExpiresOn = DateTime.UtcNow.AddDays(-1);
            await contributor.SaveChangesAsync();
            await requests.StageFileAsync(requestId, file.Id, file.FileTypeTagId, file.DisplayName, false, default);
            versionWithFileId = await CurrentVersionAsync(contributor, requestId);

            await AuthenticateAsync(contributor, tenant.AccountId);
            await CreateChangeRequestService(contributor).AddVersionAsync(requestId,
                PublishableValues(tenant, locationTag.Id, "Historical file"), false,
                tenant.RevisionId, versionWithFileId, default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            await CreateChangeRequestService(reviewer).RejectAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), "No attachment needed", default);
        }

        await using (var cleanup = database.CreateDbContext("cleanup", tenant.AccountId))
        {
            var expired = await new FileRepository(cleanup, cleanup.RequestUser).GetExpiredFiles();
            cleanup.Files.Remove(Assert.Single(expired, file => file.Id == stagedFile.FileId));
            await cleanup.SaveChangesAsync();
        }

        await using var audit = database.CreateDbContext("contributor", tenant.AccountId);
        await AuthenticateAsync(audit, tenant.AccountId);
        var historical = await CreateChangeRequestService(audit).GetVersionAsync(requestId, versionWithFileId, default);
        var unavailable = Assert.Single(historical.Proposed.Files,
            file => file.Id == stagedFile.FileId);
        Assert.Equal("Lost survey attachment", unavailable.DisplayName);
        Assert.Equal("seed-a.pdf", unavailable.FileName);
        Assert.Contains(stagedFile.FileId, historical.UnavailableStagedFileIds);
    }

    [Fact]
    public async Task PublishedFileProposalPreviewMatchesApprovedTypeAndEffectiveFileName()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PublishedFileProposalPreviewMatchesApprovedTypeAndEffectiveFileName));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var publishedFile = await TestDataBuilder.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "publish00a");
        const string mapTypeId = "maptype00a";
        await TestDataBuilder.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File, "Map", mapTypeId);
        await using (var seed = database.CreateDbContext("file-baseline", tenant.AccountId))
        {
            var reportType = await seed.TagTypes.SingleAsync(tag => tag.Id == publishedFile.FileTypeId);
            reportType.Name = "Report";
            var file = await seed.Files.SingleAsync(candidate => candidate.Id == publishedFile.FileId);
            file.FileName = "survey.pdf";
            file.DisplayName = "Survey";
            await seed.SaveChangesAsync();
            var baseline = await new CaveMutationRepository(seed, seed.RequestUser,
                    new CavePublishedSnapshotRepository(seed, seed.RequestUser))
                .PublishExistingAsync(tenant.CaveId, tenant.RevisionId, CaveRevisionSource.ManagerEdit,
                    CaveRevisionOperation.Update, _ => { });
            tenant = tenant with { RevisionId = baseline.RevisionId! };
        }
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        CaveChangePreviewVm preview;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(contributor, tenant.AccountId);
            var values = PublishableValues(tenant, locationTag.Id, "Published file metadata");
            values.Files = [new EditFileMetadataVm
            {
                Id = publishedFile.FileId,
                FileTypeTagId = mapTypeId,
                DisplayName = "Renamed Survey"
            }];
            var service = CreateChangeRequestService(contributor);
            preview = await service.PreviewAsync(tenant.CaveId, values, tenant.RevisionId, default);
            requestId = await service.CreateAsync(tenant.CaveId, values, tenant.RevisionId, default);
        }

        var proposedFile = Assert.Single(preview.Proposed.Files, file => file.Id == publishedFile.FileId);
        Assert.Equal(mapTypeId, proposedFile.FileTypeTagId);
        Assert.Equal("Map", proposedFile.FileTypeNameAtRevision);
        Assert.Equal("Renamed Survey", proposedFile.DisplayName);
        Assert.Equal("Renamed Survey.pdf", proposedFile.FileName);
        Assert.Contains(publishedFile.FileId, preview.Diff.ChangedFiles);
        Assert.Equal("Report", Assert.Single(preview.Base.Files,
            file => file.Id == publishedFile.FileId).FileTypeNameAtRevision);

        string approvedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            approvedRevisionId = (await CreateChangeRequestService(reviewer).ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), null, default)).PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == approvedRevisionId)).SnapshotJson, 1);
        var acceptedFile = Assert.Single(accepted.Files, file => file.Id == publishedFile.FileId);
        Assert.Equal(proposedFile.FileTypeTagId, acceptedFile.FileTypeTagId);
        Assert.Equal(proposedFile.FileTypeNameAtRevision, acceptedFile.FileTypeNameAtRevision);
        Assert.Equal(proposedFile.DisplayName, acceptedFile.DisplayName);
        Assert.Equal(proposedFile.FileName, acceptedFile.FileName);
    }

    [Fact]
    public async Task StagedFileProposalMetadataIsImmutableAndApprovalMatchesActiveVersion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StagedFileProposalMetadataIsImmutableAndApprovalMatchesActiveVersion));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var stagedFile = await TestDataBuilder.AddFileAsync(database, tenant, fileId: "stagedmeta");
        const string mapTypeId = "maptype00a";
        const string liveTypeId = "livetype0a";
        await TestDataBuilder.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File, "Map", mapTypeId);
        await TestDataBuilder.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File, "Live type", liveTypeId);
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string versionOneId;
        string versionTwoId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var reportType = await contributor.TagTypes.SingleAsync(tag => tag.Id == stagedFile.FileTypeId);
            reportType.Name = "Report";
            var file = await contributor.Files.SingleAsync(candidate => candidate.Id == stagedFile.FileId);
            file.FileName = "survey.pdf";
            file.DisplayName = "Survey";
            await contributor.SaveChangesAsync();
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Staged file metadata"), default);
            await requests.StageFileAsync(requestId, file.Id, file.FileTypeTagId, file.DisplayName,
                reviewer: false, default);
            versionOneId = await CurrentVersionAsync(contributor, requestId);

            await AuthenticateAsync(contributor, tenant.AccountId);
            var values = PublishableValues(tenant, locationTag.Id, "Staged file metadata");
            values.Files = [new EditFileMetadataVm
            {
                Id = stagedFile.FileId,
                FileTypeTagId = mapTypeId,
                DisplayName = "Reviewed Survey"
            }];
            versionTwoId = await CreateChangeRequestService(contributor).AddVersionAsync(requestId, values,
                againstCurrent: false, tenant.RevisionId, versionOneId, default);
        }

        await using (var inspect = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(inspect, tenant.AccountId);
            var service = CreateChangeRequestService(inspect);
            var activeRow = await inspect.CaveProposalVersions.SingleAsync(version => version.Id == versionTwoId);
            var activeIntent = Assert.Single(CaveProposalJson.Deserialize(activeRow.ProposalJson,
                activeRow.SchemaVersion).Files, intent => intent.FileId == stagedFile.FileId);
            Assert.Equal(mapTypeId, activeIntent.FileTypeTagId);
            Assert.Equal("Map", activeIntent.FileTypeName);
            Assert.Equal("Reviewed Survey.pdf", activeIntent.FileName);
            var activePreview = await service.GetVersionAsync(requestId, versionTwoId, default);
            var activeFile = Assert.Single(activePreview.Proposed.Files, file => file.Id == stagedFile.FileId);
            Assert.Equal(mapTypeId, activeFile.FileTypeTagId);
            Assert.Equal("Map", activeFile.FileTypeNameAtRevision);
            Assert.Equal("Reviewed Survey.pdf", activeFile.FileName);

            var liveFile = await inspect.Files.SingleAsync(file => file.Id == stagedFile.FileId);
            liveFile.FileTypeTagId = liveTypeId;
            liveFile.FileName = "today.txt";
            liveFile.DisplayName = "Today's mutable metadata";
            await inspect.SaveChangesAsync();

            var historical = await service.GetVersionAsync(requestId, versionOneId, default);
            var historicalFile = Assert.Single(historical.Proposed.Files,
                file => file.Id == stagedFile.FileId);
            Assert.Equal(stagedFile.FileTypeId, historicalFile.FileTypeTagId);
            Assert.Equal("Report", historicalFile.FileTypeNameAtRevision);
            Assert.Equal("survey.pdf", historicalFile.FileName);
            Assert.Equal("Survey", historicalFile.DisplayName);
            Assert.Empty(historical.UnavailableStagedFileIds);

            liveFile.FileTypeTagId = stagedFile.FileTypeId;
            liveFile.FileName = "survey.pdf";
            liveFile.DisplayName = "Survey";
            await inspect.SaveChangesAsync();
        }

        string approvedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            approvedRevisionId = (await CreateChangeRequestService(reviewer).ApproveAsync(requestId,
                versionTwoId, null, default)).PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == approvedRevisionId)).SnapshotJson, 1);
        var acceptedFile = Assert.Single(accepted.Files, file => file.Id == stagedFile.FileId);
        Assert.Equal(mapTypeId, acceptedFile.FileTypeTagId);
        Assert.Equal("Map", acceptedFile.FileTypeNameAtRevision);
        Assert.Equal("Reviewed Survey", acceptedFile.DisplayName);
        Assert.Equal("Reviewed Survey.pdf", acceptedFile.FileName);
    }

    [Fact]
    public async Task ApprovalFailsWhenActiveProposalStagedFileIsMissing()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalFailsWhenActiveProposalStagedFileIsMissing));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var stagedFile = await TestDataBuilder.AddFileAsync(database, tenant, fileId: "missing00a");
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Missing active file"), default);
            await requests.StageFileAsync(requestId, stagedFile.FileId, stagedFile.FileTypeId,
                "Required attachment", false, default);
        }
        await using (var corrupt = database.CreateDbContext("cleanup", tenant.AccountId))
        {
            await corrupt.CaveChangeRequestStagedFiles.Where(link => link.ChangeRequestId == requestId)
                .ExecuteDeleteAsync();
            await corrupt.Files.Where(file => file.Id == stagedFile.FileId).ExecuteDeleteAsync();
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            var currentVersionId = await CurrentVersionAsync(reviewer, requestId);
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                CreateChangeRequestService(reviewer).ApproveAsync(requestId,
                    currentVersionId, null, default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(request => request.Id == requestId)).Status);
        Assert.Single(await verify.CaveRevisions.Where(revision => revision.CaveId == tenant.CaveId).ToListAsync());
    }

    [Fact]
    public async Task ApprovalFailureBeforeCommitRollsBackCaveRequestRevisionAndStagedFile()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalFailureBeforeCommitRollsBackCaveRequestRevisionAndStagedFile));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await TestDataBuilder.AddFileAsync(database, tenant);
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Must roll back"), default);
            await requests.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Rollback attachment",
                reviewer: false, default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            var values = PublishableValues(tenant, locationTag.Id, "Must roll back");
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId,
                FileTypeTagId = file.FileTypeId,
                DisplayName = "Rollback attachment"
            }];
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateCaveService(reviewer)
                .ApproveChangeRequestAsync(values, tenant.RevisionId, requestId, [file.FileId],
                    (_, _) => throw new InvalidOperationException("Simulated approval failure"), default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal("Cave A", (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == tenant.CaveId)).Name);
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId)).Status);
        Assert.Equal(1, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
        Assert.Null((await verify.Files.SingleAsync(row => row.Id == file.FileId)).CaveId);
        Assert.True(await verify.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == file.FileId));
    }

    [Fact]
    public async Task ProposalVersionsAreImmutableAndApprovalPublishesExactlyOneLinkedRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalVersionsAreImmutableAndApprovalPublishesExactlyOneLinkedRevision));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await GrantViewAsync(database, tenant, "contributor");
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

    [Fact]
    public async Task RejectionDoesNotPublishAndStaleBaseCannotBePreparedForApproval()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RejectionDoesNotPublishAndStaleBaseCannotBePreparedForApproval));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await GrantViewAsync(database, tenant, "contributor");
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

    [Fact]
    public async Task StagedFileIsVersionedAndOnlyAssociatedWithCaveDuringPublication()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StagedFileIsVersionedAndOnlyAssociatedWithCaveDuringPublication));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await GrantViewAsync(database, tenant, "contributor");
        var stagedFile = await TestDataBuilder.AddFileAsync(database, tenant);
        string requestId;
        var fileId = stagedFile.FileId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var repository = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Proposal with file"), default);
            var file = await contributor.Files.SingleAsync(row => row.Id == fileId);
            file.DisplayName = "Survey";
            file.ExpiresOn = DateTime.UtcNow.AddDays(10);
            await contributor.SaveChangesAsync();
            await repository.StageFileAsync(requestId, fileId, stagedFile.FileTypeId, file.DisplayName,
                reviewer: false, default);
        }

        await using (var beforePublication = database.CreateDbContext("verify", tenant.AccountId))
        {
            var staged = await beforePublication.CaveChangeRequestStagedFiles.SingleAsync(row =>
                row.ChangeRequestId == requestId && row.FileId == fileId);
            Assert.Equal(tenant.AccountId, staged.AccountId);
            Assert.Null((await beforePublication.Files.SingleAsync(row => row.Id == fileId)).CaveId);
            var versions = await beforePublication.CaveProposalVersions
                .Where(row => row.ChangeRequestId == requestId).OrderBy(row => row.CreatedOn).ToListAsync();
            Assert.Equal(2, versions.Count);
            var proposal = CaveProposalJson.Deserialize(versions[^1].ProposalJson, versions[^1].SchemaVersion);
            Assert.Contains(proposal.Files, intent => intent.FileId == fileId &&
                intent.Disposition == ProposalFileDisposition.PublishStaged);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            var caves = new Planarian.Modules.Caves.Repositories.CaveRepository(reviewer, reviewer.RequestUser);
            var attached = await caves.AttachStagedFilesAsync(requestId, tenant.CaveId, [fileId]);
            await reviewer.SaveChangesAsync();
            Assert.Single(attached);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var published = await verify.Files.SingleAsync(row => row.Id == fileId);
        Assert.Equal(tenant.CaveId, published.CaveId);
        Assert.Null(published.ExpiresOn);
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == fileId));
    }

    [Fact]
    public async Task RemovedStagedFileIsNotPublishedAndTerminalApprovalClearsAllStagingLinks()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RemovedStagedFileIsNotPublishedAndTerminalApprovalClearsAllStagingLinks));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var removed = await TestDataBuilder.AddFileAsync(database, tenant);
        var retained = new TestFileData("retain0001", removed.FileTypeId);
        await using (var seed = database.CreateDbContext("file-seed", tenant.AccountId))
        {
            seed.Files.Add(new Planarian.Model.Database.Entities.RidgeWalker.File
            {
                Id = retained.FileId, AccountId = tenant.AccountId, FileTypeTagId = retained.FileTypeId,
                FileName = "retained.pdf", BlobKey = "retained", BlobContainer = "test"
            });
            await seed.SaveChangesAsync();
        }
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var repository = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "File choices"), default);
            foreach (var fileId in new[] { removed.FileId, retained.FileId })
            {
                var file = await contributor.Files.SingleAsync(row => row.Id == fileId);
                file.ExpiresOn = DateTime.UtcNow.AddDays(-1);
                await contributor.SaveChangesAsync();
                await repository.StageFileAsync(requestId, fileId, file.FileTypeTagId, file.DisplayName,
                    reviewer: false, default);
            }
            var expectedVersionId = (await contributor.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
            var values = PublishableValues(tenant, locationTag.Id, "File choices");
            values.Files = [new EditFileMetadataVm
                { Id = retained.FileId, FileTypeTagId = retained.FileTypeId, DisplayName = "Retained" }];
            await AuthenticateAsync(contributor, tenant.AccountId);
            await CreateChangeRequestService(contributor).AddVersionAsync(requestId, values, false,
                tenant.RevisionId, expectedVersionId, default);
            var detail = await CreateChangeRequestService(contributor).GetAsync(requestId, default);
            Assert.DoesNotContain(detail.Proposed.Files, file => file.Id == removed.FileId);
            Assert.Contains(detail.Proposed.Files, file => file.Id == retained.FileId);
        }

        await using (var pending = database.CreateDbContext("verify", tenant.AccountId))
        {
            var request = await pending.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
            var version = await pending.CaveProposalVersions.SingleAsync(row => row.Id == request.CurrentProposalVersionId);
            var proposal = CaveProposalJson.Deserialize(version.ProposalJson, version.SchemaVersion);
            Assert.DoesNotContain(proposal.Files, intent => intent.FileId == removed.FileId);
            Assert.Contains(proposal.Files, intent => intent.FileId == retained.FileId &&
                intent.Disposition == ProposalFileDisposition.PublishStaged);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            Assert.Equal(CaveChangeRequestDecisionResult.Approved,
                (await CreateChangeRequestService(reviewer).ApproveAsync(requestId,
                    await CurrentVersionAsync(reviewer, requestId), null, default)).Result);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Null((await verify.Files.SingleAsync(row => row.Id == removed.FileId)).CaveId);
        Assert.Equal(tenant.CaveId, (await verify.Files.SingleAsync(row => row.Id == retained.FileId)).CaveId);
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(row => row.ChangeRequestId == requestId));
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(row =>
            row.ChangeRequestId == requestId)).SnapshotJson, 1);
        Assert.DoesNotContain(accepted.Files, file => file.Id == removed.FileId);
        Assert.Contains(accepted.Files, file => file.Id == retained.FileId);
        Assert.Contains(await new FileRepository(verify, verify.RequestUser).GetExpiredFiles(),
            file => file.Id == removed.FileId);
    }

    [Fact]
    public async Task AutomaticCountyMovePublishesAllocatedNumberAndRevisionRecordsIt()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AutomaticCountyMovePublishesAllocatedNumberAndRevisionRecordsIt));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        const string newCountyId = "county999a";
        await using (var seed = database.CreateDbContext("county-seed", tenant.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = newCountyId, AccountId = tenant.AccountId, StateId = tenant.StateId,
                DisplayId = "A99", Name = "New County"
            });
            await seed.SaveChangesAsync();
        }
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var proposal = PublishableProposal(tenant, locationTag.Id, "Moved Cave") with
            {
                CountyId = newCountyId,
                CountyNumberIntent = CountyNumberIntent.AutomaticNext,
                RequestedCountyNumber = null
            };
            requestId = await new CaveChangeRequestRepository(contributor, contributor.RequestUser)
                .CreateAsync(tenant.CaveId, tenant.RevisionId, proposal, default);
            await AuthenticateAsync(contributor, tenant.AccountId);
            var detail = await CreateChangeRequestService(contributor).GetAsync(requestId, default);
            Assert.Equal(CountyNumberIntent.AutomaticNext, detail.CountyNumberIntent);
            Assert.Null(detail.RequestedCountyNumber);
        }

        string revisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            revisionId = (await CreateChangeRequestService(reviewer).ApproveAsync(requestId,
                    await CurrentVersionAsync(reviewer, requestId), null, default))
                .PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal(newCountyId, cave.CountyId);
        Assert.Equal(1, cave.CountyNumber);
        var snapshot = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(row => row.Id == revisionId))
            .SnapshotJson, 1);
        Assert.Equal(newCountyId, snapshot.County.Id);
        Assert.Equal(1, snapshot.CountyNumber);
    }

    public static IEnumerable<object?[]> MeasurementPreservationCases()
    {
        yield return [null, null, null, null];
        yield return [0d, 0d, 0d, 0];
        yield return [null, 0d, 27d, 3];
    }

    [Theory]
    [MemberData(nameof(MeasurementPreservationCases))]
    public async Task UnrelatedProposalEditPreservesNullableMeasurementSemantics(
        double? length, double? depth, double? maxPitDepth, int? numberOfPits)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(UnrelatedProposalEditPreservesNullableMeasurementSemantics)}_{length}_{depth}_{maxPitDepth}_{numberOfPits}");
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a',
            length, depth, maxPitDepth, numberOfPits);
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(contributor, tenant.AccountId);
            var context = await CreateChangeRequestService(contributor).GetAuthoringContextAsync(tenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.Name = "Unrelated name edit";
            requestId = await CreateChangeRequestService(contributor).CreateAsync(tenant.CaveId, values,
                context.ExpectedBaseRevisionId, default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            await CreateChangeRequestService(reviewer).ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), null, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal(length, cave.LengthFeet);
        Assert.Equal(depth, cave.DepthFeet);
        Assert.Equal(maxPitDepth, cave.MaxPitDepthFeet);
        Assert.Equal(numberOfPits, cave.NumberOfPits);
        var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == cave.CurrentRevisionId);
        var snapshot = CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);
        Assert.Equal(length, snapshot.LengthFeet);
        Assert.Equal(depth, snapshot.DepthFeet);
        Assert.Equal(maxPitDepth, snapshot.MaxPitDepthFeet);
        Assert.Equal(numberOfPits, snapshot.NumberOfPits);
    }

    [Fact]
    public async Task ProposalCanIntentionallyTransitionMeasurementsBetweenNullZeroAndPositive()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalCanIntentionallyTransitionMeasurementsBetweenNullZeroAndPositive));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, 25, 0, null);
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(contributor, tenant.AccountId);
            var context = await CreateChangeRequestService(contributor).GetAuthoringContextAsync(tenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.LengthFeet = 10;
            values.DepthFeet = null;
            values.MaxPitDepthFeet = null;
            values.NumberOfPits = 0;
            requestId = await CreateChangeRequestService(contributor).CreateAsync(tenant.CaveId, values,
                context.ExpectedBaseRevisionId, default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            await CreateChangeRequestService(reviewer).ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), null, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal(10, cave.LengthFeet);
        Assert.Null(cave.DepthFeet);
        Assert.Null(cave.MaxPitDepthFeet);
        Assert.Equal(0, cave.NumberOfPits);
    }

    [Fact]
    public async Task ImportedUnknownMeasurementsDoNotBecomeSyntheticZeroProposalChanges()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ImportedUnknownMeasurementsDoNotBecomeSyntheticZeroProposalChanges));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        string importedCaveId;
        string importedRevisionId;
        await using (var importer = database.CreateDbContext("importer", tenant.AccountId))
        {
            var harness = new CaveImportTestHarness(importer, importer.RequestUser);
            var csv = ImportDryRunIntegrationTests.CaveHeader + "\n" +
                      "Imported Unknowns,County A,A01,20,AA,,,,,,,,,,,,,,,false,,\n";
            var plan = await harness.PlanCsvAsync(csv, syncExisting: false);
            importedCaveId = Assert.Single(plan.Caves).Id;
            await harness.ExecuteAsync(plan, "unknown-measurements.csv");
            importer.ChangeTracker.Clear();
            importedRevisionId = (await importer.Caves.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == importedCaveId)).CurrentRevisionId!;
        }
        var imported = tenant with
        {
            CaveId = importedCaveId,
            CaveName = "Imported Unknowns",
            CountyNumber = 20,
            RevisionId = importedRevisionId
        };
        await GrantViewAsync(database, imported, "contributor");

        await using var contributor = database.CreateDbContext("contributor", tenant.AccountId);
        await AuthenticateAsync(contributor, tenant.AccountId);
        var service = CreateChangeRequestService(contributor);
        var context = await service.GetAuthoringContextAsync(importedCaveId, default);
        var values = ValuesFromCave(context.Cave);
        values.Name = "Imported Unknowns Renamed";

        var preview = await service.PreviewAsync(importedCaveId, values, importedRevisionId, default);
        var requestId = await service.CreateAsync(importedCaveId, values, importedRevisionId, default);

        Assert.Equal(["Name"], preview.Diff.Scalars.Select(change => change.Path));
        var stored = await contributor.CaveProposalVersions.SingleAsync(row => row.ChangeRequestId == requestId);
        var proposal = CaveProposalJson.Deserialize(stored.ProposalJson, stored.SchemaVersion);
        Assert.Null(proposal.LengthFeet);
        Assert.Null(proposal.DepthFeet);
        Assert.Null(proposal.MaxPitDepthFeet);
        Assert.Null(proposal.NumberOfPits);
    }

    [Fact]
    public async Task DirectEditRacingAfterApprovalLoadReturnsConflictAndRollsBackPublication()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectEditRacingAfterApprovalLoadReturnsConflictAndRollsBackPublication));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await TestDataBuilder.AddFileAsync(database, tenant);
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string expectedVersionId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(contributor, tenant.AccountId);
            requestId = await CreateChangeRequestService(contributor).CreateAsync(tenant.CaveId,
                PublishableValues(tenant, locationTag.Id, "Approval loses race"), tenant.RevisionId, default);
            await new CaveChangeRequestRepository(contributor, contributor.RequestUser).StageFileAsync(
                requestId, file.FileId, file.FileTypeId, "Still staged", false, default);
            expectedVersionId = await CurrentVersionAsync(contributor, requestId);
        }

        var pause = new PauseCaveUpdateInterceptor();
        await using var approving = database.CreateDbContext("reviewer", tenant.AccountId, pause);
        await AuthenticateAsync(approving, tenant.AccountId);
        var approvalTask = CreateChangeRequestService(approving).ApproveAsync(requestId,
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
        await AuthenticateAsync(retry, tenant.AccountId);
        var stale = await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
            CreateChangeRequestService(retry).ApproveAsync(requestId, expectedVersionId, null, default));
        Assert.Equal(managerRevisionId, stale.ActualRevisionId);
    }

    [Fact]
    public async Task UnauthorizedAndCrossTenantIdentifiersCannotAccessOrDecideRequests()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(UnauthorizedAndCrossTenantIdentifiersCannotAccessOrDecideRequests));
        var tenantA = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var tenantB = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');
        var locationTagA = await TestDataBuilder.AddTagAsync(database, tenantA.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade A", "locqual00a");
        await GrantViewAsync(database, tenantA, "contributor");
        await GrantViewAsync(database, tenantA, "viewer");

        string requestId;
        string versionId;
        await using (var contributor = database.CreateDbContext("contributor", tenantA.AccountId))
        {
            await AuthenticateAsync(contributor, tenantA.AccountId);
            requestId = await CreateChangeRequestService(contributor).CreateAsync(tenantA.CaveId,
                PublishableValues(tenantA, locationTagA.Id, "Protected proposal"), tenantA.RevisionId, default);
            versionId = await CurrentVersionAsync(contributor, requestId);
        }

        await using (var viewer = database.CreateDbContext("viewer", tenantA.AccountId))
        {
            await AuthenticateAsync(viewer, tenantA.AccountId);
            var service = CreateChangeRequestService(viewer);
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.ApproveAsync(requestId, versionId, null, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.RejectAsync(requestId, versionId, null, default));
        }

        await using (var accountB = database.CreateDbContext("user-b", tenantB.AccountId))
        {
            await EnsureAccountUserAsync(accountB, tenantB.AccountId);
            await AuthenticateAsync(accountB, tenantB.AccountId);
            var service = CreateChangeRequestService(accountB);
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => service.GetAsync(requestId, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.GetVersionAsync(requestId, versionId, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.AddVersionAsync(requestId, new AddCaveVm { Id = tenantA.CaveId }, false,
                    tenantA.RevisionId, versionId, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.ApproveAsync(requestId, versionId, null, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.RejectAsync(requestId, versionId, null, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.OpenStagedFileAsync(requestId, "guessedfile", default));
        }

        await using var verify = database.CreateDbContext("verify", tenantA.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Pending, request.Status);
        Assert.Null(request.ReviewerUserId);
        Assert.Single(await verify.CaveRevisions.Where(row => row.CaveId == tenantA.CaveId).ToListAsync());
    }

    [Fact]
    public async Task GuessedInvisibleCaveAndMismatchedStagedFileAreNotAccessible()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(GuessedInvisibleCaveAndMismatchedStagedFileAreNotAccessible));
        var visible = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var invisible = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');
        var locationTag = await TestDataBuilder.AddTagAsync(database, visible.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await TestDataBuilder.AddFileAsync(database, visible);
        await GrantViewAsync(database, visible, "contributor");

        await using var contributor = database.CreateDbContext("contributor", visible.AccountId);
        await AuthenticateAsync(contributor, visible.AccountId);
        var service = CreateChangeRequestService(contributor);
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => service.CreateAsync(
            invisible.CaveId, PublishableValues(invisible, locationTag.Id, "Guessed cave"),
            invisible.RevisionId, default));

        var requestA = await service.CreateAsync(visible.CaveId,
            PublishableValues(visible, locationTag.Id, "Request A"), visible.RevisionId, default);
        var requestB = await service.CreateAsync(visible.CaveId,
            PublishableValues(visible, locationTag.Id, "Request B"), visible.RevisionId, default);
        await new CaveChangeRequestRepository(contributor, contributor.RequestUser).StageFileAsync(
            requestB, file.FileId, file.FileTypeId, "Request B file", false, default);

        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            service.OpenStagedFileAsync(requestA, file.FileId, default));
        contributor.ChangeTracker.Clear();
        Assert.Equal(2, await contributor.CaveChangeRequests.CountAsync());
        Assert.Equal(3, await contributor.CaveProposalVersions.CountAsync());

        await using (var crossAccount = database.CreateDbContext("cross-account", invisible.AccountId))
        {
            await EnsureAccountUserAsync(crossAccount, invisible.AccountId);
            await AuthenticateAsync(crossAccount, invisible.AccountId);
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                CreateChangeRequestService(crossAccount).OpenStagedFileAsync(requestB, file.FileId, default));
        }

        await using var verifyInvisible = database.CreateDbContext("verify", invisible.AccountId);
        Assert.Empty(await verifyInvisible.CaveChangeRequests.ToListAsync());
        Assert.Empty(await verifyInvisible.CaveProposalVersions.ToListAsync());
    }

    private static CaveProposalSnapshotV1 Proposal(PublishedCaveTestData cave, string name) => new()
    {
        AccountId = cave.AccountId,
        CaveId = cave.CaveId,
        Name = name,
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        CountyNumberIntent = CountyNumberIntent.Manual,
        RequestedCountyNumber = cave.CountyNumber
    };

    private static CaveProposalSnapshotV1 PublishableProposal(PublishedCaveTestData cave,
        string locationQualityTagId, string name, string? narrative = null) => new()
    {
        AccountId = cave.AccountId,
        CaveId = cave.CaveId,
        Name = name,
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        CountyNumberIntent = CountyNumberIntent.Manual,
        RequestedCountyNumber = cave.CountyNumber,
        Narrative = narrative,
        Entrances = [new CaveProposalEntranceV1
        {
            EntranceId = string.Empty,
            Name = "Main Entrance",
            IsPrimary = true,
            Latitude = 35,
            Longitude = -86,
            Elevation = 500,
            LocationQualityTagId = locationQualityTagId
        }]
    };

    private static AddCaveVm PublishableValues(PublishedCaveTestData cave, string locationQualityTagId,
        string name, string? narrative = null) => new()
    {
        Id = cave.CaveId,
        Name = name,
        AlternateNames = [],
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        CountyNumber = cave.CountyNumber,
        IsCountyNumberManuallySet = true,
        Narrative = narrative,
        Entrances = [new AddEntranceVm
        {
            Name = "Main Entrance",
            IsPrimary = true,
            Latitude = 35,
            Longitude = -86,
            ElevationFeet = 500,
            LocationQualityTagId = locationQualityTagId
        }]
    };

    private static AddCaveVm ValuesFromCave(CaveVm cave) => new()
    {
        Id = cave.Id,
        Name = cave.Name,
        AlternateNames = cave.AlternateNames,
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        CountyNumber = cave.CountyNumber,
        IsCountyNumberManuallySet = true,
        LengthFeet = cave.LengthFeet,
        DepthFeet = cave.DepthFeet,
        MaxPitDepthFeet = cave.MaxPitDepthFeet,
        NumberOfPits = cave.NumberOfPits,
        Narrative = cave.Narrative,
        ReportedOn = cave.ReportedOn,
        GeologyTagIds = cave.GeologyTagIds,
        ReportedByNameTagIds = cave.ReportedByNameTagIds,
        BiologyTagIds = cave.BiologyTagIds,
        ArcheologyTagIds = cave.ArcheologyTagIds,
        CartographerNameTagIds = cave.CartographerNameTagIds,
        MapStatusTagIds = cave.MapStatusTagIds,
        GeologicAgeTagIds = cave.GeologicAgeTagIds,
        PhysiographicProvinceTagIds = cave.PhysiographicProvinceTagIds,
        OtherTagIds = cave.OtherTagIds,
        Files = cave.Files.Select(file => new EditFileMetadataVm
            { Id = file.Id, FileTypeTagId = file.FileTypeTagId, DisplayName = file.DisplayName }).ToList(),
        Entrances = cave.Entrances.Select(entrance => new AddEntranceVm
        {
            Id = entrance.Id,
            IsPrimary = entrance.IsPrimary,
            LocationQualityTagId = entrance.LocationQualityTagId,
            Name = entrance.Name,
            Description = entrance.Description,
            Latitude = entrance.Latitude,
            Longitude = entrance.Longitude,
            ElevationFeet = entrance.ElevationFeet,
            ReportedOn = entrance.ReportedOn,
            PitFeet = entrance.PitFeet,
            EntranceStatusTagIds = entrance.EntranceStatusTagIds,
            FieldIndicationTagIds = entrance.FieldIndicationTagIds,
            EntranceHydrologyTagIds = entrance.EntranceHydrologyTagIds,
            EntranceOtherTagIds = entrance.EntranceOtherTagIds,
            ReportedByNameTagIds = entrance.ReportedByNameTagIds
        }).ToList()
    };

    private static CaveChangeRequestService CreateChangeRequestService(Planarian.Model.Database.PlanarianDbContext db)
    {
        var user = db.RequestUser;
        var caves = new CaveRepository(db, user);
        var mutations = new CaveMutationCoordinator(new CaveMutationRepository(db, user,
            new CavePublishedSnapshotRepository(db, user)));
        return new CaveChangeRequestService(new CaveChangeRequestRepository(db, user), caves, CreateCaveService(db),
            new CaveRevisionQueryRepository(db, user), mutations, null!, user);
    }

    private static async Task<(PublishedCaveTestData Cave, string LocationTagId)> CreateMeasuredPublishedCaveAsync(
        PostgresTestDatabase database, char suffix, double? length, double? depth, double? maxPitDepth,
        int? numberOfPits)
    {
        var cave = await TestDataBuilder.CreatePublishedCaveAsync(database, suffix);
        var locationTag = await TestDataBuilder.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", $"locqual00{suffix}");
        await TestDataBuilder.AddEntranceAsync(database, cave, $"entrance0{suffix}",
            locationQualityTagId: locationTag.Id);
        string revisionId;
        await using (var manager = database.CreateDbContext("measurement-seed", cave.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            revisionId = (await mutations.PublishExistingAsync(cave.CaveId, cave.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, entity =>
                {
                    entity.LengthFeet = length;
                    entity.DepthFeet = depth;
                    entity.MaxPitDepthFeet = maxPitDepth;
                    entity.NumberOfPits = numberOfPits;
                })).RevisionId!;
        }
        return (cave with { RevisionId = revisionId }, locationTag.Id);
    }

    private static CaveService CreateCaveService(Planarian.Model.Database.PlanarianDbContext db)
    {
        var user = db.RequestUser;
        var snapshots = new CavePublishedSnapshotRepository(db, user);
        var coordinator = new CaveMutationCoordinator(new CaveMutationRepository(db, user, snapshots));
        return new CaveService(new CaveRepository(db, user), user, null!, new TagRepository(db, user), null!, null!,
            coordinator);
    }

    private static FileService CreateFileService(Planarian.Model.Database.PlanarianDbContext db)
    {
        var user = db.RequestUser;
        var snapshots = new CavePublishedSnapshotRepository(db, user);
        var coordinator = new CaveMutationCoordinator(new CaveMutationRepository(db, user, snapshots));
        return new FileService(new FileRepository(db, user), user, new TagRepository(db, user), null!, null!,
            new CaveRepository(db, user), null!, coordinator);
    }

    private static Task<string> CurrentVersionAsync(Planarian.Model.Database.PlanarianDbContext db,
        string requestId) => db.CaveChangeRequests.Where(request => request.Id == requestId)
        .Select(request => request.CurrentProposalVersionId!).SingleAsync();

    private static Task<int> BackendPidAsync(Planarian.Model.Database.PlanarianDbContext db) =>
        db.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();

    private static async Task AssertBlockedByDatabaseLockAsync(PostgresTestDatabase database, string accountId,
        int backendPid, Task operation)
    {
        await using var observer = database.CreateDbContext("lock-observer", accountId);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!timeout.IsCancellationRequested)
        {
            var blockerCount = await observer.Database.SqlQuery<int>(
                    $"SELECT cardinality(pg_blocking_pids({backendPid})) AS \"Value\"")
                .SingleAsync(timeout.Token);
            if (blockerCount > 0)
            {
                Assert.False(operation.IsCompleted);
                return;
            }
            await Task.Delay(10, timeout.Token);
        }
        throw new Xunit.Sdk.XunitException("The operation never entered a PostgreSQL lock wait.");
    }

    private static async Task GrantViewAsync(PostgresTestDatabase database, PublishedCaveTestData cave, string userId)
    {
        await using var db = database.CreateDbContext(userId, cave.AccountId);
        await EnsureAccountUserAsync(db, cave.AccountId);
        const string permissionId = "vIeWPz9a00";
        if (!await db.Permissions.AnyAsync(row => row.Id == permissionId))
        {
            db.Permissions.Add(new Permission
            {
                Id = permissionId, Key = "View", Name = "View", Description = "View Caves",
                PermissionType = "Cave"
            });
            await db.SaveChangesAsync();
        }
        db.CavePermissions.Add(new CavePermission
        {
            UserId = db.RequestUser.Id,
            AccountId = cave.AccountId,
            CaveId = cave.CaveId,
            PermissionId = permissionId
        });
        await db.SaveChangesAsync();
    }

    private static async Task GrantManagerAsync(PostgresTestDatabase database, PublishedCaveTestData cave,
        string userId)
    {
        await using var db = database.CreateDbContext(userId, cave.AccountId);
        await EnsureAccountUserAsync(db, cave.AccountId);
        const string permissionId = "mAnageR000";
        if (!await db.Permissions.AnyAsync(row => row.Id == permissionId))
        {
            db.Permissions.Add(new Permission
            {
                Id = permissionId, Key = "Manager", Name = "Manager", Description = "Manage Caves",
                PermissionType = "Cave"
            });
            await db.SaveChangesAsync();
        }
        db.CavePermissions.Add(new CavePermission
        {
            UserId = db.RequestUser.Id,
            AccountId = cave.AccountId,
            CaveId = cave.CaveId,
            PermissionId = permissionId
        });
        await db.SaveChangesAsync();
    }

    private static async Task EnsureAccountUserAsync(Planarian.Model.Database.PlanarianDbContext db,
        string accountId)
    {
        if (await db.AccountUsers.AnyAsync(row => row.AccountId == accountId && row.UserId == db.RequestUser.Id))
            return;
        db.AccountUsers.Add(new AccountUser
        {
            AccountId = accountId,
            UserId = db.RequestUser.Id,
            InvitationAcceptedOn = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static Task AuthenticateAsync(Planarian.Model.Database.PlanarianDbContext db, string accountId) =>
        db.RequestUser.Initialize(accountId, db.RequestUser.Id);
}

internal sealed class PauseCaveUpdateInterceptor : DbCommandInterceptor
{
    public TaskCompletionSource CaveUpdateReached { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ContinueUpdate { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await PauseIfCaveUpdateAsync(command, cancellationToken);
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await PauseIfCaveUpdateAsync(command, cancellationToken);
        return result;
    }

    private async Task PauseIfCaveUpdateAsync(DbCommand command, CancellationToken cancellationToken)
    {
        if (!command.CommandText.Contains("UPDATE \"Caves\"", StringComparison.Ordinal)) return;
        CaveUpdateReached.TrySetResult();
        await ContinueUpdate.Task.WaitAsync(cancellationToken);
    }
}
