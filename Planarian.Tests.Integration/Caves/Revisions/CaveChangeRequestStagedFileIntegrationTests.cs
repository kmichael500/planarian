using Microsoft.EntityFrameworkCore;
using System.Text;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Repositories;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveChangeRequestStagedFileIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
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

        var staged = await contributor.Services.StageRequestFileForTestAsync(requestId, content, "notes.txt", null, default);
        await contributor.Services.Files.DeleteUnpublishedFileAsync(staged.Id, default);
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
        var staged = await contributor.Services.StageRequestFileForTestAsync(firstRequest, content, "notes.txt", null, default);

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
        var staged = await contributorA.Services.StageRequestFileForTestAsync(requestId, content, "notes.txt", null, default);
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
            contributor.Services.StageRequestFileForTestAsync(requestId, content, "notes.txt", null, default));

        Assert.Empty(blobs.Keys);
        Assert.False(await contributor.Db.Files.AnyAsync(file => file.CaveId == null));
        Assert.False(await contributor.Db.CaveChangeRequestStagedFiles.AnyAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectionCommitsRelationalCleanupBeforeBestEffortBlobDeletion(bool failBlobDelete)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(RejectionCommitsRelationalCleanupBeforeBestEffortBlobDeletion)}_{failBlobDelete}");
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var location = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await ReferenceTestData.AddTagAsync(database, cave.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await CavePermissions.GrantManagerAsync(database, cave, "reviewer");
        var blobs = new TestFileBlobStore();
        string requestId;
        string stagedFileId;

        await using (var contributor = await CaveTestActor.CreateAsync(database, cave.AccountId,
                         "contributor", blobs))
        {
            requestId = await contributor.ChangeRequests.CreateAsync(cave.CaveId,
                PublishableValues(cave, location.Id, "Rejected attachment"), cave.RevisionId, default);
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("survey bytes"));
            stagedFileId = (await contributor.Services.StageRequestFileForTestAsync(requestId, content,
                "survey.pdf", null, default)).Id;
        }

        Assert.Single(blobs.Keys);
        blobs.FailDeletes = failBlobDelete;
        await using (var reviewer = await CaveTestActor.CreateAsync(database, cave.AccountId, "reviewer", blobs))
        {
            var currentVersionId = await CurrentVersionAsync(reviewer.Db, requestId);
            var decision = await reviewer.ChangeRequests.RejectAsync(requestId, currentVersionId,
                "Not accepted", default);
            Assert.Equal(CaveChangeRequestDecisionResult.Rejected, decision.Result);
            var detail = await reviewer.ChangeRequests.GetAsync(requestId, default);
            Assert.Contains(detail.Proposed.Files, file => file.Id == stagedFileId &&
                file.DisplayName == "survey");
            Assert.Contains(stagedFileId, detail.UnavailableStagedFileIds);
            Assert.DoesNotContain(detail.ActiveStagedFiles, file => file.Id == stagedFileId);
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(CaveChangeRequestStatus.Rejected,
            (await verify.CaveChangeRequests.SingleAsync(request => request.Id == requestId)).Status);
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(link => link.FileId == stagedFileId));
        Assert.False(await verify.Files.AnyAsync(file => file.Id == stagedFileId));
        Assert.Equal(failBlobDelete ? 1 : 0, blobs.Keys.Count);
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
        Assert.False(await afterRejection.CaveChangeRequestStagedFiles
            .AnyAsync(link => link.FileId == stagedFile.FileId));
        Assert.False(await afterRejection.Files.AnyAsync(file => file.Id == stagedFile.FileId));
    }

    [Fact]
    public async Task RejectingOneRequestRetainsAnUnpublishedFileStillStagedByAnotherRequest()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RejectingOneRequestRetainsAnUnpublishedFileStillStagedByAnotherRequest));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        var stagedFile = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "shared000a");
        string firstRequestId;
        string secondRequestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            firstRequestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "First shared-file proposal"), default);
            secondRequestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Second shared-file proposal"), default);
            await requests.StageFileAsync(firstRequestId, stagedFile.FileId, stagedFile.FileTypeId,
                "Shared attachment", reviewer: false, default);
            await requests.StageFileAsync(secondRequestId, stagedFile.FileId, stagedFile.FileTypeId,
                "Shared attachment", reviewer: false, default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
            await new CaveChangeRequestRepository(reviewer, reviewer.RequestUser).RejectAsync(firstRequestId,
                await CurrentVersionAsync(reviewer, firstRequestId), "Reject only the first", default);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.True(await verify.Files.AnyAsync(file => file.Id == stagedFile.FileId));
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(link =>
            link.ChangeRequestId == firstRequestId && link.FileId == stagedFile.FileId));
        Assert.True(await verify.CaveChangeRequestStagedFiles.AnyAsync(link =>
            link.ChangeRequestId == secondRequestId && link.FileId == stagedFile.FileId));
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

        await using var audit = database.CreateDbContext("contributor", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(audit, tenant.AccountId);
        var historical = await IntegrationTestServices.For(audit).CaveChangeRequests.GetVersionAsync(requestId, versionWithFileId, default);
        var unavailable = Assert.Single(historical.Proposed.Files,
            file => file.Id == stagedFile.FileId);
        Assert.Equal("Lost survey attachment", unavailable.DisplayName);
        Assert.Equal("seed-a.pdf", unavailable.FileName);
        Assert.Contains(stagedFile.FileId, historical.UnavailableStagedFileIds);
    }
}
