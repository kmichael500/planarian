using Microsoft.EntityFrameworkCore;
using System.Text;
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

public sealed class CaveChangeRequestFileIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ContributorCanStageAndOpenFileThroughChangeRequestService()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ContributorCanStageAndOpenFileThroughChangeRequestService));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var location = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await ReferenceTestData.AddTagAsync(database, cave.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        var blobs = new TestFileBlobStore();
        await using var contributor = await CaveTestActor.CreateAsync(database, cave.AccountId, "contributor", blobs);
        var requestId = await contributor.ChangeRequests.CreateAsync(cave.CaveId,
            PublishableValues(cave, location.Id, "Proposal with a staged file"), cave.RevisionId, default);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("staged cave document"));

        var staged = await contributor.ChangeRequests.StageFileAsync(requestId, content, "notes.txt", null, default);
        var opened = await contributor.ChangeRequests.OpenStagedFileAsync(requestId, staged.Id, default);

        using var reader = new StreamReader(opened.Stream);
        Assert.Equal("notes.txt", opened.FileName);
        Assert.Equal("staged cave document", await reader.ReadToEndAsync());
        Assert.True(await contributor.Db.CaveChangeRequestStagedFiles
            .AnyAsync(link => link.ChangeRequestId == requestId && link.FileId == staged.Id));
    }

    [Fact]
    public async Task StagedFileBelongingToAnotherRequestCannotBeOpened()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StagedFileBelongingToAnotherRequestCannotBeOpened));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var location = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await ReferenceTestData.AddTagAsync(database, cave.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await using var contributor = await CaveTestActor.CreateAsync(database, cave.AccountId, "contributor");
        var firstRequest = await contributor.ChangeRequests.CreateAsync(cave.CaveId,
            PublishableValues(cave, location.Id, "First proposal"), cave.RevisionId, default);
        var secondRequest = await new CaveChangeRequestRepository(contributor.Db, contributor.Db.RequestUser)
            .CreateAsync(cave.CaveId, cave.RevisionId, Proposal(cave, "Second proposal"), default);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("private staged content"));
        var staged = await contributor.ChangeRequests.StageFileAsync(firstRequest, content, "notes.txt", null, default);

        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.OpenStagedFileAsync(secondRequest, staged.Id, default));

        Assert.Contains("Staged file", failure.Message);
    }

    [Fact]
    public async Task CrossTenantActorCannotOpenStagedFile()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(CrossTenantActorCannotOpenStagedFile));
        var caveA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var caveB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var location = await ReferenceTestData.AddTagAsync(database, caveA.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await ReferenceTestData.AddTagAsync(database, caveA.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, caveA, "contributor-a");
        await CavePermissions.GrantViewAsync(database, caveB, "contributor-b");
        var blobs = new TestFileBlobStore();
        await using var contributorA = await CaveTestActor.CreateAsync(database, caveA.AccountId, "contributor-a", blobs);
        var requestId = await contributorA.ChangeRequests.CreateAsync(caveA.CaveId,
            PublishableValues(caveA, location.Id, "Tenant A proposal"), caveA.RevisionId, default);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("tenant A content"));
        var staged = await contributorA.ChangeRequests.StageFileAsync(requestId, content, "notes.txt", null, default);
        await using var contributorB = await CaveTestActor.CreateAsync(database, caveB.AccountId, "contributor-b", blobs);

        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributorB.ChangeRequests.OpenStagedFileAsync(requestId, staged.Id, default));
    }

    [Fact]
    public async Task FailedBlobWriteLeavesNoStagedFileOrRelationship()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(FailedBlobWriteLeavesNoStagedFileOrRelationship));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var location = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await ReferenceTestData.AddTagAsync(database, cave.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        var blobs = new TestFileBlobStore { FailWrites = true };
        await using var contributor = await CaveTestActor.CreateAsync(database, cave.AccountId, "contributor", blobs);
        var requestId = await contributor.ChangeRequests.CreateAsync(cave.CaveId,
            PublishableValues(cave, location.Id, "Proposal with failed file"), cave.RevisionId, default);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("will fail"));

        await Assert.ThrowsAsync<IOException>(() =>
            contributor.ChangeRequests.StageFileAsync(requestId, content, "notes.txt", null, default));

        Assert.Empty(blobs.Keys);
        Assert.False(await contributor.Db.Files.AnyAsync(file => file.CaveId == null));
        Assert.False(await contributor.Db.CaveChangeRequestStagedFiles.AnyAsync());
    }

    [Fact]
    public async Task FileOnlyProposalVersionCanBeApprovedEndToEnd()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(FileOnlyProposalVersionCanBeApprovedEndToEnd));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string fileOnlyVersionId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
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
            Assert.Equal([file.FileId], detail.DiffFromBase.AddedFiles);
            Assert.Empty(detail.DiffFromBase.Scalars);
        }

        CaveChangeRequestDecisionVm decision;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            decision = await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId, fileOnlyVersionId,
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
    public async Task ApplicationApprovalPublishesStagedFileInsideSingleRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApplicationApprovalPublishesStagedFileInsideSingleRevision));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await FileTestDataFactory.AddFileAsync(database, tenant);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
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
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var stagedVersionId = (await contributor.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
            var values = PublishableValues(tenant, locationTag.Id, "Proposal with staged file");
            values.Files = [new EditFileMetadataVm
                { Id = file.FileId, FileTypeTagId = file.FileTypeId, DisplayName = "Survey attachment" }];
            await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(requestId,
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
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            result = await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
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
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "boundary0a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
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
            await CavePermissions.AuthenticateAsync(manager, tenant.AccountId);
            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                IntegrationTestServices.For(manager).Files.UpdateFilesMetadata([new EditFileMetadataVm
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
            await CavePermissions.AuthenticateAsync(manager, tenant.AccountId);
            await IntegrationTestServices.For(manager).Files.UpdateFilesMetadata([new EditFileMetadataVm
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
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "approve00a");
        var mapType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Map", "filemap00a");
        var laterType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Photograph", "filephotoa");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
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
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            proposalVersionId = await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(requestId, values,
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
            await CavePermissions.AuthenticateAsync(concurrentManager, tenant.AccountId);
            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                IntegrationTestServices.For(concurrentManager).Files.UpdateFilesMetadata([new EditFileMetadataVm
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
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            acceptedRevisionId = (await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
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
            await CavePermissions.AuthenticateAsync(manager, tenant.AccountId);
            await IntegrationTestServices.For(manager).Files.UpdateFilesMetadata([new EditFileMetadataVm
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
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "publish00a");
        var mapType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
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
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var cave = await new CaveRepository(contributor, contributor.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            values.Entrances = PublishableValues(tenant, locationTag.Id, cave!.Name).Entrances;
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId, FileTypeTagId = mapType.Id, DisplayName = "Survey Map"
            }];
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
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
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            var detail = await IntegrationTestServices.For(reviewer).CaveChangeRequests.GetAsync(requestId, default);
            var reviewed = Assert.Single(detail.Proposed.Files);
            Assert.Equal((mapType.Id, "Map", "Survey Map.pdf"),
                (reviewed.FileTypeTagId, reviewed.FileTypeNameAtRevision, reviewed.FileName));
            approvedRevisionId = (await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
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
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "stgtype00a");
        var mapType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Map", "filemap00a");
        await using (var seed = database.CreateDbContext("staged-file-type-seed", tenant.AccountId))
        {
            (await seed.TagTypes.SingleAsync(tag => tag.Id == file.FileTypeId)).Name = "Report";
            (await seed.Files.SingleAsync(row => row.Id == file.FileId)).FileName = "survey.pdf";
            await seed.SaveChangesAsync();
        }
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
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
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            versionId = await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(requestId, values,
                againstCurrent: false, expectedBaseRevisionId: tenant.RevisionId,
                expectedProposalVersionId: stagedVersionId, default);
        }

        await using (var inspect = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(inspect, tenant.AccountId);
            var stored = await inspect.CaveProposalVersions.SingleAsync(version => version.Id == versionId);
            var intent = Assert.Single(CaveProposalJson.Deserialize(stored.ProposalJson, stored.SchemaVersion).Files,
                candidate => candidate.FileId == file.FileId);
            Assert.Equal((mapType.Id, "Map", "Survey Map.pdf"),
                (intent.FileTypeTagId, intent.FileTypeName, intent.FileName));
            var historical = await IntegrationTestServices.For(inspect).CaveChangeRequests.GetVersionAsync(requestId, versionId, default);
            var presented = Assert.Single(historical.Proposed.Files);
            Assert.Equal((mapType.Id, "Map", "Survey Map.pdf"),
                (presented.FileTypeTagId, presented.FileTypeNameAtRevision, presented.FileName));
        }

        string approvedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            approvedRevisionId = (await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId, versionId,
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
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "drift0000a");
        var mapType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Map", "filemap00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
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
        await CavePermissions.AuthenticateAsync(audit, tenant.AccountId);
        var historical = await IntegrationTestServices.For(audit).CaveChangeRequests.GetVersionAsync(requestId, versionId, default);
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
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        var stagedFile = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "staged000a");
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
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var stagedFile = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "history00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
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

            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(requestId,
                PublishableValues(tenant, locationTag.Id, "Historical file"), false,
                tenant.RevisionId, versionWithFileId, default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            await IntegrationTestServices.For(reviewer).CaveChangeRequests.RejectAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), "No attachment needed", default);
        }

        await using (var cleanup = database.CreateDbContext("cleanup", tenant.AccountId))
        {
            var expired = await new FileRepository(cleanup, cleanup.RequestUser).GetExpiredFiles();
            cleanup.Files.Remove(Assert.Single(expired, file => file.Id == stagedFile.FileId));
            await cleanup.SaveChangesAsync();
        }

        await using var audit = database.CreateDbContext("contributor", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(audit, tenant.AccountId);
        var historical = await IntegrationTestServices.For(audit).CaveChangeRequests.GetVersionAsync(requestId, versionWithFileId, default);
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
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var publishedFile = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "publish00a");
        const string mapTypeId = "maptype00a";
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File, "Map", mapTypeId);
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
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        CaveChangePreviewVm preview;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var values = PublishableValues(tenant, locationTag.Id, "Published file metadata");
            values.Files = [new EditFileMetadataVm
            {
                Id = publishedFile.FileId,
                FileTypeTagId = mapTypeId,
                DisplayName = "Renamed Survey"
            }];
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
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
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            approvedRevisionId = (await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
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
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var stagedFile = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "stagedmeta");
        const string mapTypeId = "maptype00a";
        const string liveTypeId = "livetype0a";
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File, "Map", mapTypeId);
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File, "Live type", liveTypeId);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

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

            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var values = PublishableValues(tenant, locationTag.Id, "Staged file metadata");
            values.Files = [new EditFileMetadataVm
            {
                Id = stagedFile.FileId,
                FileTypeTagId = mapTypeId,
                DisplayName = "Reviewed Survey"
            }];
            versionTwoId = await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(requestId, values,
                againstCurrent: false, tenant.RevisionId, versionOneId, default);
        }

        await using (var inspect = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(inspect, tenant.AccountId);
            var service = IntegrationTestServices.For(inspect).CaveChangeRequests;
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
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            approvedRevisionId = (await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
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
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var stagedFile = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "missing00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
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
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            var currentVersionId = await CurrentVersionAsync(reviewer, requestId);
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
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
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await FileTestDataFactory.AddFileAsync(database, tenant);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
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
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            var values = PublishableValues(tenant, locationTag.Id, "Must roll back");
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId,
                FileTypeTagId = file.FileTypeId,
                DisplayName = "Rollback attachment"
            }];
            await Assert.ThrowsAsync<InvalidOperationException>(() => IntegrationTestServices.For(reviewer).Caves
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
    public async Task StagedFileIsVersionedAndOnlyAssociatedWithCaveDuringPublication()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StagedFileIsVersionedAndOnlyAssociatedWithCaveDuringPublication));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        var stagedFile = await FileTestDataFactory.AddFileAsync(database, tenant);
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
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var removed = await FileTestDataFactory.AddFileAsync(database, tenant);
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
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
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
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(requestId, values, false,
                tenant.RevisionId, expectedVersionId, default);
            var detail = await IntegrationTestServices.For(contributor).CaveChangeRequests.GetAsync(requestId, default);
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
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            Assert.Equal(CaveChangeRequestDecisionResult.Approved,
                (await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
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
}
