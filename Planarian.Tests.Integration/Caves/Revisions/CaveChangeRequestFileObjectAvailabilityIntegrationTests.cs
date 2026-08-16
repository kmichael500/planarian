using Microsoft.EntityFrameworkCore;
using System.Text;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Revisions;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveChangeRequestFileObjectAvailabilityIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ApprovalFailsWhenActiveProposalStagedFileRecordIsMissing()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalFailsWhenActiveProposalStagedFileRecordIsMissing));
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
    public async Task ApprovalPublishesTheSameImmutableStagedObjectWithoutCopying()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalPublishesTheSameImmutableStagedObjectWithoutCopying));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var location = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        var blobs = new TestFileBlobStore();
        var originalBytes = Encoding.UTF8.GetBytes("published survey bytes");
        string requestId;
        string versionId;
        string fileId;
        string objectKey;
        string partition;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId,
                         "contributor", blobs))
        {
            requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, location.Id, "Proposal with immutable bytes"), tenant.RevisionId, default);
            await using var content = new MemoryStream(originalBytes);
            fileId = (await contributor.Services.StageRequestFileForTestAsync(requestId, content,
                "survey-map.pdf", null, default)).Id;
            versionId = await CurrentVersionAsync(contributor.Db, requestId);
            var staged = await contributor.Db.Files.SingleAsync(file => file.Id == fileId);
            objectKey = staged.BlobKey!;
            partition = staged.BlobContainer!;
            Assert.Equal($"objects/files/{fileId}", objectKey);
            Assert.Null(staged.CaveId);
            Assert.NotNull(staged.ExpiresOn);
            Assert.True(blobs.Contains(partition, objectKey));
            Assert.Single(blobs.Keys);
        }

        CaveChangeRequestDecisionVm decision;
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer", blobs))
            decision = await reviewer.ChangeRequests.ApproveAsync(requestId, versionId, null, default);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var published = await verify.Files.SingleAsync(file => file.Id == fileId);
        Assert.Equal(tenant.CaveId, published.CaveId);
        Assert.Null(published.ExpiresOn);
        Assert.Equal((partition, objectKey), (published.BlobContainer, published.BlobKey));
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(link => link.FileId == fileId));
        Assert.Single(blobs.Keys);
        Assert.True(blobs.Contains(partition, objectKey));
        Assert.Equal(originalBytes, blobs.Read(partition, objectKey));
        var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == decision.PublishedRevisionId);
        Assert.Contains(CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion).Files,
            file => file.Id == fileId);
    }

    [Fact]
    public async Task ApprovalRejectsMissingStagedObjectBeforePublishingRelationalState()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalRejectsMissingStagedObjectBeforePublishingRelationalState));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var location = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        var blobs = new TestFileBlobStore();
        string requestId;
        string versionId;
        string fileId;
        string objectKey;
        string partition;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId,
                         "contributor", blobs))
        {
            requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, location.Id, "Missing physical bytes"), tenant.RevisionId, default);
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("soon missing"));
            fileId = (await contributor.Services.StageRequestFileForTestAsync(requestId, content,
                "survey.pdf", null, default)).Id;
            versionId = await CurrentVersionAsync(contributor.Db, requestId);
            var staged = await contributor.Db.Files.SingleAsync(file => file.Id == fileId);
            objectKey = staged.BlobKey!;
            partition = staged.BlobContainer!;
        }
        blobs.Delete(partition, objectKey);

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer", blobs))
        {
            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                reviewer.ChangeRequests.ApproveAsync(requestId, versionId, null, default));
            Assert.Contains("no longer available", failure.Message);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        var stagedFile = await verify.Files.SingleAsync(file => file.Id == fileId);
        Assert.Equal(CaveChangeRequestStatus.Pending, request.Status);
        Assert.Equal(versionId, request.CurrentProposalVersionId);
        Assert.Equal(tenant.RevisionId, cave.CurrentRevisionId);
        Assert.Single(await verify.CaveRevisions.Where(revision => revision.CaveId == tenant.CaveId).ToListAsync());
        Assert.Null(stagedFile.CaveId);
        Assert.Equal(objectKey, stagedFile.BlobKey);
        Assert.True(await verify.CaveChangeRequestStagedFiles.AnyAsync(link =>
            link.ChangeRequestId == requestId && link.FileId == fileId));
    }

    [Fact]
    public async Task ApprovalRejectsAStagedFileSharedWithAnotherPendingRequest()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalRejectsAStagedFileSharedWithAnotherPendingRequest));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var location = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string firstRequestId;
        string secondRequestId;
        string firstVersionId;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            firstRequestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, location.Id, "First shared approval"), tenant.RevisionId, default);
            var requests = new CaveChangeRequestRepository(contributor.Db, contributor.Db.RequestUser);
            secondRequestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, location.Id, "Second shared approval"), default);
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("shared bytes"));
            var staged = await contributor.Services.StageRequestFileForTestAsync(firstRequestId, content,
                "shared.pdf", null, default);
            contributor.Db.CaveChangeRequestStagedFiles.Add(new CaveChangeRequestStagedFile
            {
                AccountId = tenant.AccountId,
                ChangeRequestId = secondRequestId,
                FileId = staged.Id
            });
            await contributor.Db.SaveChangesAsync();
            firstVersionId = await CurrentVersionAsync(contributor.Db, firstRequestId);
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                reviewer.ChangeRequests.ApproveAsync(firstRequestId, firstVersionId, null, default));
            Assert.Contains("shared with another pending request", failure.Message);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == firstRequestId)).Status);
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == secondRequestId)).Status);
        var sharedFileId = (await verify.CaveChangeRequestStagedFiles
            .SingleAsync(link => link.ChangeRequestId == firstRequestId)).FileId;
        Assert.Null((await verify.Files.SingleAsync(row => row.Id == sharedFileId)).CaveId);
        Assert.Equal(2, await verify.CaveChangeRequestStagedFiles.CountAsync(link => link.FileId == sharedFileId));
    }
}
