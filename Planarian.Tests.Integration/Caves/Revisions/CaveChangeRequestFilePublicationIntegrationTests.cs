using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Files.Repositories;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveChangeRequestFilePublicationIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task PublishedFileBlobDeleteFailureDoesNotUndoCommittedCaveRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PublishedFileBlobDeleteFailureDoesNotUndoCommittedCaveRevision));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var location = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        var blobs = new TestFileBlobStore();
        string fileId;
        string blobKey;
        string blobContainer;
        string revisionWithFileId;

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer", blobs))
        {
            await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("published bytes"));
            fileId = (await manager.Services.Files.UploadCaveFile(content, tenant.CaveId, "published.pdf",
                default)).Id;
            var file = await manager.Db.Files.SingleAsync(row => row.Id == fileId);
            blobKey = file.BlobKey!;
            blobContainer = file.BlobContainer!;
            revisionWithFileId = (await manager.Db.Caves.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == tenant.CaveId)).CurrentRevisionId!;
            blobs.FailDelete(blobContainer, blobKey);

            var values = PublishableValues(tenant, location.Id, tenant.CaveName);
            values.Files = [];
            Assert.Equal(tenant.CaveId, await manager.Services.Caves.AddCave(values, default));
        }

        Assert.True(blobs.Contains(blobContainer, blobKey));
        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.False(await verify.Files.AnyAsync(row => row.Id == fileId));
        var caveRow = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.NotEqual(revisionWithFileId, caveRow.CurrentRevisionId);
        var previous = await verify.CaveRevisions.SingleAsync(row => row.Id == revisionWithFileId);
        var current = await verify.CaveRevisions.SingleAsync(row => row.Id == caveRow.CurrentRevisionId);
        Assert.Contains(CaveSnapshotJson.Deserialize(previous.SnapshotJson, previous.SnapshotSchemaVersion).Files,
            file => file.Id == fileId);
        Assert.DoesNotContain(CaveSnapshotJson.Deserialize(current.SnapshotJson, current.SnapshotSchemaVersion).Files,
            file => file.Id == fileId);
    }

    [Fact]
    public async Task FileOnlyProposalVersionCanBeApprovedEndToEnd()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(FileOnlyProposalVersionCanBeApprovedEndToEnd));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant);
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", System.Text.Encoding.UTF8.GetBytes("file-only bytes"));
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string fileOnlyVersionId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var service = contributor.ChangeRequests;
            var context = await service.GetAuthoringContextAsync(tenant.CaveId, default);
            var initialValues = ValuesFromCave(context.Cave);
            initialValues.Name = "Temporary field change";
            requestId = await service.CreateAsync(tenant.CaveId, initialValues, tenant.RevisionId, default);
            var repository = new CaveChangeRequestRepository(contributor.Db, contributor.Db.RequestUser);
            await repository.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Only file change", false,
                default);
            var stagedVersionId = await CurrentVersionAsync(contributor.Db, requestId);
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
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer", blobs))
        {
            decision = await reviewer.ChangeRequests.ApproveAsync(requestId, fileOnlyVersionId,
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
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", System.Text.Encoding.UTF8.GetBytes("application approval bytes"));
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
            result = await IntegrationTestServices.For(reviewer, blobs).CaveChangeRequests.ApproveAsync(requestId,
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
                .ApproveChangeRequestAsync(values, tenant.RevisionId, requestId,
                    [new StagedCaveFilePublication(file.FileId, "seed-a", "test")],
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
            var attached = await caves.AttachStagedFilesAsync(requestId, tenant.CaveId,
                [new StagedCaveFilePublication(fileId, "seed-a", "test")]);
            await reviewer.SaveChangesAsync();
            Assert.Single(attached);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var published = await verify.Files.SingleAsync(row => row.Id == fileId);
        Assert.Equal(tenant.CaveId, published.CaveId);
        Assert.Null(published.ExpiresOn);
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == fileId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApprovalCommitsDiscardedStagedFileCleanupBeforeBestEffortBlobDeletion(bool failBlobDelete)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalCommitsDiscardedStagedFileCleanupBeforeBestEffortBlobDeletion) +
            (failBlobDelete ? "_delete_failure" : "_delete_success"));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var fileType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        var blobs = new TestFileBlobStore();
        var removedBytes = System.Text.Encoding.UTF8.GetBytes("removed bytes");
        var retainedBytes = System.Text.Encoding.UTF8.GetBytes("retained bytes");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string versionContainingRemovedFile;
        string acceptedVersionId;
        string removedFileId;
        string retainedFileId;
        string removedSourceKey;
        string removedContainer;
        string retainedSourceKey;
        string retainedContainer;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId,
                         "contributor", blobs))
        {
            requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, locationTag.Id, "File choices"), tenant.RevisionId, default);
            await using var removedContent = new MemoryStream(removedBytes);
            removedFileId = (await contributor.Services.StageRequestFileForTestAsync(requestId, removedContent,
                "removed.pdf", null, default)).Id;
            await using var retainedContent = new MemoryStream(retainedBytes);
            retainedFileId = (await contributor.Services.StageRequestFileForTestAsync(requestId, retainedContent,
                "retained.pdf", null, default)).Id;

            var stagedVersionId = await CurrentVersionAsync(contributor.Db, requestId);
            var bothValues = PublishableValues(tenant, locationTag.Id, "File choices");
            bothValues.Files =
            [
                new EditFileMetadataVm
                {
                    Id = removedFileId, FileTypeTagId = fileType.Id, DisplayName = "Removed survey"
                },
                new EditFileMetadataVm
                {
                    Id = retainedFileId, FileTypeTagId = fileType.Id, DisplayName = "Retained survey"
                }
            ];
            versionContainingRemovedFile = await contributor.ChangeRequests.AddVersionAsync(requestId, bothValues,
                false, tenant.RevisionId, stagedVersionId, default);

            var retainedValues = PublishableValues(tenant, locationTag.Id, "Accepted file choices");
            retainedValues.Files =
            [
                new EditFileMetadataVm
                {
                    Id = retainedFileId, FileTypeTagId = fileType.Id, DisplayName = "Retained survey"
                }
            ];
            acceptedVersionId = await contributor.ChangeRequests.AddVersionAsync(requestId, retainedValues,
                false, tenant.RevisionId, versionContainingRemovedFile, default);

            var removedFile = await contributor.Db.Files.SingleAsync(row => row.Id == removedFileId);
            removedSourceKey = removedFile.BlobKey!;
            removedContainer = removedFile.BlobContainer!;
            var retainedFile = await contributor.Db.Files.SingleAsync(row => row.Id == retainedFileId);
            retainedSourceKey = retainedFile.BlobKey!;
            retainedContainer = retainedFile.BlobContainer!;
        }

        await using (var pending = database.CreateDbContext("verify", tenant.AccountId))
        {
            Assert.True(await pending.Files.AnyAsync(file => file.Id == removedFileId));
            Assert.True(await pending.Files.AnyAsync(file => file.Id == retainedFileId));
            Assert.True(await pending.CaveChangeRequestStagedFiles.AnyAsync(link =>
                link.ChangeRequestId == requestId && link.FileId == removedFileId));
            Assert.True(await pending.CaveChangeRequestStagedFiles.AnyAsync(link =>
                link.ChangeRequestId == requestId && link.FileId == retainedFileId));
            Assert.True(blobs.Contains(removedContainer, removedSourceKey));
            Assert.True(blobs.Contains(retainedContainer, retainedSourceKey));
            var current = await pending.CaveProposalVersions.SingleAsync(row => row.Id == acceptedVersionId);
            var currentProposal = CaveProposalJson.Deserialize(current.ProposalJson, current.SchemaVersion);
            Assert.DoesNotContain(currentProposal.Files, intent => intent.FileId == removedFileId);
            var historical = await pending.CaveProposalVersions.SingleAsync(
                row => row.Id == versionContainingRemovedFile);
            Assert.Contains(CaveProposalJson.Deserialize(historical.ProposalJson, historical.SchemaVersion).Files,
                intent => intent.FileId == removedFileId);
        }

        if (failBlobDelete) blobs.FailDelete(removedContainer, removedSourceKey);

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer", blobs))
        {
            var decision = await reviewer.ChangeRequests.ApproveAsync(requestId, acceptedVersionId, null, default);
            Assert.Equal(CaveChangeRequestDecisionResult.Approved, decision.Result);
        }

        await using (var verify = database.CreateDbContext("verify", tenant.AccountId))
        {
            Assert.Equal(CaveChangeRequestStatus.Approved,
                (await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId)).Status);
            Assert.False(await verify.Files.AnyAsync(file => file.Id == removedFileId));
            Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(
                row => row.ChangeRequestId == requestId));
            var retained = await verify.Files.SingleAsync(file => file.Id == retainedFileId);
            Assert.Equal(tenant.CaveId, retained.CaveId);
            Assert.Null(retained.ExpiresOn);
            Assert.Equal(retainedSourceKey, retained.BlobKey);
            Assert.Equal(retainedContainer, retained.BlobContainer);
            Assert.True(blobs.Contains(retainedContainer, retainedSourceKey));
            var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(row =>
                row.ChangeRequestId == requestId)).SnapshotJson, 1);
            Assert.DoesNotContain(accepted.Files, file => file.Id == removedFileId);
            Assert.Contains(accepted.Files, file => file.Id == retainedFileId);
        }

        if (failBlobDelete)
            Assert.True(blobs.Contains(removedContainer, removedSourceKey));
        else
            Assert.False(blobs.Contains(removedContainer, removedSourceKey));

        await using var audit = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor", blobs);
        var historicalVersion = await audit.ChangeRequests.GetVersionAsync(requestId,
            versionContainingRemovedFile, default);
        var historicalFile = Assert.Single(historicalVersion.Proposed.Files, file => file.Id == removedFileId);
        Assert.Equal("Removed survey.pdf", historicalFile.FileName);
        Assert.Equal("Removed survey", historicalFile.DisplayName);
        Assert.Equal(fileType.Id, historicalFile.FileTypeTagId);
        Assert.Equal("Other", historicalFile.FileTypeNameAtRevision);
        Assert.Contains(removedFileId, historicalVersion.UnavailableStagedFileIds);
    }

    [Fact]
    public async Task ApprovalCleanupRetainsDiscardedStagedFileStillReferencedByAnotherRequest()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalCleanupRetainsDiscardedStagedFileStillReferencedByAnotherRequest));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        var blobs = new TestFileBlobStore();
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string firstRequestId;
        string secondRequestId;
        string acceptedVersionId;
        string sharedFileId;
        string sourceKey;
        string container;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId,
                         "contributor", blobs))
        {
            firstRequestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, locationTag.Id, "First request"), tenant.RevisionId, default);
            secondRequestId = await new CaveChangeRequestRepository(contributor.Db, contributor.Db.RequestUser)
                .CreateAsync(tenant.CaveId, tenant.RevisionId,
                    PublishableProposal(tenant, locationTag.Id, "Second request"), default);
            await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("shared bytes"));
            sharedFileId = (await contributor.Services.StageRequestFileForTestAsync(firstRequestId, content,
                "shared.pdf", null, default)).Id;
            contributor.Db.CaveChangeRequestStagedFiles.Add(new CaveChangeRequestStagedFile
            {
                AccountId = tenant.AccountId,
                ChangeRequestId = secondRequestId,
                FileId = sharedFileId
            });
            await contributor.Db.SaveChangesAsync();

            var stagedVersionId = await CurrentVersionAsync(contributor.Db, firstRequestId);
            acceptedVersionId = await contributor.ChangeRequests.AddVersionAsync(firstRequestId,
                PublishableValues(tenant, locationTag.Id, "First request without shared file"), false,
                tenant.RevisionId, stagedVersionId, default);
            var shared = await contributor.Db.Files.SingleAsync(file => file.Id == sharedFileId);
            sourceKey = shared.BlobKey!;
            container = shared.BlobContainer!;
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer", blobs))
            Assert.Equal(CaveChangeRequestDecisionResult.Approved,
                (await reviewer.ChangeRequests.ApproveAsync(firstRequestId, acceptedVersionId, null, default)).Result);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(CaveChangeRequestStatus.Approved,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == firstRequestId)).Status);
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == secondRequestId)).Status);
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(link =>
            link.ChangeRequestId == firstRequestId && link.FileId == sharedFileId));
        Assert.True(await verify.CaveChangeRequestStagedFiles.AnyAsync(link =>
            link.ChangeRequestId == secondRequestId && link.FileId == sharedFileId));
        var sharedFile = await verify.Files.SingleAsync(file => file.Id == sharedFileId);
        Assert.Null(sharedFile.CaveId);
        Assert.Equal(sourceKey, sharedFile.BlobKey);
        Assert.True(blobs.Contains(container, sourceKey));
    }
}
