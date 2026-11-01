using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
using Planarian.Tests.Integration.Infrastructure.Concurrency;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

using Planarian.Modules.Files.Persistence;
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
        await contributor.Services.Files.DeleteAuthoringStagedFileAsync(staged.Id, default);
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
        Assert.False(await contributor.Db.StorageCleanupTargets.AnyAsync(target =>
            target.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectionCommitsRelationalCleanupWithDurableObjectCleanupDebt(bool failBlobDelete)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(RejectionCommitsRelationalCleanupWithDurableObjectCleanupDebt)}_{failBlobDelete}");
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
                file.Name == "survey");
            Assert.Contains(stagedFileId, detail.UnavailableStagedFileIds);
            Assert.DoesNotContain(detail.ActiveStagedFiles, file => file.Id == stagedFileId);
        }

        string? cleanupId = null;
        await using (var verify = database.CreateDbContext("verify", cave.AccountId))
        {
            Assert.Equal(CaveChangeRequestStatus.Rejected,
                (await verify.CaveChangeRequests.SingleAsync(request => request.Id == requestId)).Status);
            Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(link => link.FileId == stagedFileId));
            Assert.False(await verify.Files.AnyAsync(file => file.Id == stagedFileId));
            var cleanup = await verify.StorageCleanupTargets.AsNoTracking()
                .Where(row => row.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval)
                .ToListAsync();
            if (failBlobDelete)
            {
                cleanupId = Assert.Single(cleanup).Id;
                Assert.Single(blobs.Keys);
            }
            else
            {
                Assert.Empty(cleanup);
                Assert.Empty(blobs.Keys);
            }
        }

        if (cleanupId is not null)
        {
            blobs.FailDeletes = false;
            await using (var retry = await CaveTestActor.CreateAsync(
                             database, cave.AccountId, "reviewer", blobs))
                await retry.Services.StorageCleanup.CleanupUnpublishedFileTargetsAsync([cleanupId], default);

            await using var cleaned = database.CreateDbContext("verify-cleaned", cave.AccountId);
            Assert.False(await cleaned.StorageCleanupTargets.AnyAsync(row => row.Id == cleanupId));
            Assert.Empty(blobs.Keys);
        }
    }

    [Fact]
    public async Task PendingStagedFileIsExcludedFromExpirationUntilRequestIsRejected()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PendingStagedFileIsExcludedFromExpirationUntilRequestIsRejected));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        var blobs = new TestFileBlobStore();
        string requestId;
        string stagedFileId;
        string unrelatedFileId;

        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, tenant.AccountId, "contributor", blobs))
        {
            var requests = new CaveChangeRequestRepository(contributor.Db, contributor.Db.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Pending files"), default);

            await using var stagedContent = new MemoryStream(Encoding.UTF8.GetBytes("claimed"));
            var staged = await contributor.Services.StageRequestFileForTestAsync(
                requestId, stagedContent, "staged.pdf", null, default);
            stagedFileId = staged.Id;

            await using var unrelatedContent = new MemoryStream(Encoding.UTF8.GetBytes("unclaimed"));
            var unrelated = await contributor.Services.Files.StageAuthoringFile(
                unrelatedContent, "expired.pdf", default);
            unrelatedFileId = unrelated.Id;

            await contributor.Db.Files.IgnoreQueryFilters()
                .Where(file => file.Id == stagedFileId || file.Id == unrelatedFileId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(file => file.ExpiresOn, DateTime.UtcNow.AddDays(-1)));

            var cleanupTargets = await new AuthoringFileStore(
                    contributor.Db, contributor.Db.RequestUser, TimeProvider.System)
                .RemoveExpiredAsync(16, default);
            contributor.Db.ChangeTracker.Clear();
            Assert.True(await contributor.Db.Files.AnyAsync(file => file.Id == stagedFileId));
            Assert.False(await contributor.Db.Files.AnyAsync(file => file.Id == unrelatedFileId));
            Assert.Single(cleanupTargets);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
            await new CaveChangeRequestRepository(reviewer, reviewer.RequestUser)
                .RejectAsync(requestId, await CurrentVersionAsync(reviewer, requestId), "Rejected", default);

        await using var afterRejection = database.CreateDbContext("verify", tenant.AccountId);
        Assert.False(await afterRejection.CaveChangeRequestStagedFiles
            .AnyAsync(link => link.FileId == stagedFileId));
        Assert.False(await afterRejection.Files.AnyAsync(file => file.Id == stagedFileId));
    }

    [Fact]
    public async Task ProposalClaimRechecksEachFilesExpirationAfterWaiting()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalClaimRechecksEachFilesExpirationAfterWaiting));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, cave.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        var blobs = new TestFileBlobStore();

        var intents = new List<ProposalFileIntent>();
        string secondFileId;
        await using (var author = await CaveTestActor.CreateAsync(
                         database, cave.AccountId, "contributor", blobs))
        {
            await using var firstContent = new MemoryStream(Encoding.UTF8.GetBytes("first claim"));
            var first = await author.Services.Files.StageAuthoringFile(firstContent, "first.pdf", default);
            await using var secondContent = new MemoryStream(Encoding.UTF8.GetBytes("second claim"));
            var second = await author.Services.Files.StageAuthoringFile(secondContent, "second.pdf", default);

            var rows = await author.Db.Files.Where(file => file.Id == first.Id || file.Id == second.Id)
                .OrderBy(file => file.Id)
                .ToListAsync();
            rows[0].ExpiresOn = DateTime.UtcNow.AddMinutes(5);
            rows[1].ExpiresOn = DateTime.UtcNow.AddMinutes(5);
            secondFileId = rows[1].Id;
            await author.Db.SaveChangesAsync();

            intents.AddRange(rows.Select(row =>
                new ProposalFileIntent(row.Id, ProposalFileDisposition.PublishStaged,
                    row.FileTypeTagId, row.Name, row.Extension, "Other")));
        }

        var proposal = Proposal(cave, "Expiration wait") with { Files = intents };
        var pause = new PauseBeforeCommandInterceptor("AND \"CreatedByUserId\" =");
        await using var claimDb = database.CreateDbContext("contributor", cave.AccountId, pause);
        var claimTask = new CaveChangeRequestRepository(claimDb, claimDb.RequestUser)
            .CreateAsync(cave.CaveId, cave.RevisionId, proposal, default);

        await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var forcedExpiration = DateTime.UtcNow.AddSeconds(2);
        await using (var expire = database.CreateDbContext("expire", cave.AccountId))
        {
            var changed = await expire.Files.IgnoreQueryFilters().Where(file => file.Id == secondFileId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(file => file.ExpiresOn, forcedExpiration));
            Assert.Equal(1, changed);
            Assert.Equal(forcedExpiration,
                await expire.Files.IgnoreQueryFilters().AsNoTracking()
                    .Where(file => file.Id == secondFileId)
                    .Select(file => file.ExpiresOn)
                    .SingleAsync());
        }

        var waitForExpiration = forcedExpiration - DateTime.UtcNow + TimeSpan.FromMilliseconds(100);
        if (waitForExpiration > TimeSpan.Zero)
            await Task.Delay(waitForExpiration);

        pause.Continue();
        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => claimTask);
        Assert.Equal(404, failure.StatusCode);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.False(await verify.CaveChangeRequests.AnyAsync(request => request.CaveId == cave.CaveId));
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync());
        Assert.Equal(2, await verify.Files.CountAsync(file => intents.Select(intent => intent.FileId).Contains(file.Id)));
        Assert.Equal(2, blobs.Keys.Count);
    }

    [Fact]
    public async Task ProposalClaimWinsRaceAgainstAuthoringStagedDeletion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalClaimWinsRaceAgainstAuthoringStagedDeletion));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, cave.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        var blobs = new TestFileBlobStore();

        string fileId;
        string fileTypeTagId;
        string fileName;
        string extension;
        string partition;
        string storageKey;
        await using (var author = await CaveTestActor.CreateAsync(
                         database, cave.AccountId, "contributor", blobs))
        {
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("claim wins"));
            var staged = await author.Services.Files.StageAuthoringFile(content, "claim-wins.pdf", default);
            var row = await author.Db.Files.AsNoTracking().SingleAsync(file => file.Id == staged.Id);
            fileId = row.Id;
            fileTypeTagId = row.FileTypeTagId;
            fileName = row.Name;
            extension = row.Extension;
            partition = row.BlobContainer!;
            storageKey = row.BlobKey!;
        }

        var proposal = Proposal(cave, "Claim wins") with
        {
            Files =
            [
                new ProposalFileIntent(fileId, ProposalFileDisposition.PublishStaged,
                    fileTypeTagId, fileName, extension, "Other")
            ]
        };

        var claimPause = new PauseBeforeCommandInterceptor("INSERT INTO \"CaveChangeRequestStagedFiles\"");
        await using var claimDb = database.CreateDbContext("contributor", cave.AccountId, claimPause);
        var claimTask = new CaveChangeRequestRepository(claimDb, claimDb.RequestUser)
            .CreateAsync(cave.CaveId, cave.RevisionId, proposal, default);
        await claimPause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var deletionDb = database.CreateDbContext("contributor", cave.AccountId);
        await deletionDb.Database.OpenConnectionAsync();
        var deletionPid = await PostgresLockAssertions.BackendPidAsync(deletionDb);
        var deletionTask = IntegrationTestServices.For(deletionDb, blobs).Files
            .DeleteAuthoringStagedFileAsync(fileId, default);
        await PostgresLockAssertions.AssertBlockedAsync(
            database, cave.AccountId, deletionPid, deletionTask);

        claimPause.Continue();
        var requestId = await claimTask;
        await deletionTask;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.True(await verify.Files.AnyAsync(file => file.Id == fileId));
        Assert.True(await verify.CaveChangeRequestStagedFiles.AnyAsync(link =>
            link.AccountId == cave.AccountId && link.ChangeRequestId == requestId && link.FileId == fileId));
        Assert.True(blobs.Contains(partition, storageKey));
        Assert.False(await verify.StorageCleanupTargets.AnyAsync(target =>
            target.StoragePartition == partition && target.StorageKey == storageKey));
    }

    [Fact]
    public async Task AuthoringStagedDeletionWinsRaceAgainstProposalClaim()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AuthoringStagedDeletionWinsRaceAgainstProposalClaim));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, cave.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        var blobs = new TestFileBlobStore();

        string fileId;
        string fileTypeTagId;
        string fileName;
        string extension;
        string partition;
        string storageKey;
        await using (var author = await CaveTestActor.CreateAsync(
                         database, cave.AccountId, "contributor", blobs))
        {
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("delete wins"));
            var staged = await author.Services.Files.StageAuthoringFile(content, "delete-wins.pdf", default);
            var row = await author.Db.Files.AsNoTracking().SingleAsync(file => file.Id == staged.Id);
            fileId = row.Id;
            fileTypeTagId = row.FileTypeTagId;
            fileName = row.Name;
            extension = row.Extension;
            partition = row.BlobContainer!;
            storageKey = row.BlobKey!;
        }

        var proposal = Proposal(cave, "Delete wins") with
        {
            Files =
            [
                new ProposalFileIntent(fileId, ProposalFileDisposition.PublishStaged,
                    fileTypeTagId, fileName, extension, "Other")
            ]
        };

        var deletePause = new PauseBeforeCommandInterceptor("DELETE FROM \"Files\"");
        await using var deletionDb = database.CreateDbContext("contributor", cave.AccountId, deletePause);
        var deletionTask = IntegrationTestServices.For(deletionDb, blobs).Files
            .DeleteAuthoringStagedFileAsync(fileId, default);
        await deletePause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var claimDb = database.CreateDbContext("contributor", cave.AccountId);
        await claimDb.Database.OpenConnectionAsync();
        var claimPid = await PostgresLockAssertions.BackendPidAsync(claimDb);
        var claimTask = new CaveChangeRequestRepository(claimDb, claimDb.RequestUser)
            .CreateAsync(cave.CaveId, cave.RevisionId, proposal, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, claimPid, claimTask);

        deletePause.Continue();
        await deletionTask;
        var claimFailure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => claimTask);
        Assert.Equal(404, claimFailure.StatusCode);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.False(await verify.Files.IgnoreQueryFilters().AnyAsync(file => file.Id == fileId));
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(link => link.FileId == fileId));
        Assert.False(await verify.CaveChangeRequests.AnyAsync(request => request.CaveId == cave.CaveId));
        Assert.False(blobs.Contains(partition, storageKey));
        Assert.False(await verify.StorageCleanupTargets.AnyAsync(target =>
            target.StoragePartition == partition && target.StorageKey == storageKey));
    }

    [Fact]
    public async Task ExpiredCleanupObservesProposalClaimCommittedWhileWaitingForFileLock()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ExpiredCleanupObservesProposalClaimCommittedWhileWaitingForFileLock));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, cave.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        var blobs = new TestFileBlobStore();

        string fileId;
        string fileTypeTagId;
        string fileName;
        string extension;
        DateTime expiresOn;
        await using (var author = await CaveTestActor.CreateAsync(
                         database, cave.AccountId, "contributor", blobs))
        {
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("claim race"));
            var staged = await author.Services.Files.StageAuthoringFile(content, "claim-race.pdf", default);
            var row = await author.Db.Files.SingleAsync(file => file.Id == staged.Id);
            expiresOn = DateTime.UtcNow.AddSeconds(2);
            row.ExpiresOn = expiresOn;
            await author.Db.SaveChangesAsync();
            fileId = row.Id;
            fileTypeTagId = row.FileTypeTagId;
            fileName = row.Name;
            extension = row.Extension;
        }

        var proposal = Proposal(cave, "Cleanup claim race") with
        {
            Files =
            [
                new ProposalFileIntent(fileId, ProposalFileDisposition.PublishStaged,
                    fileTypeTagId, fileName, extension, "Other")
            ]
        };

        var pause = new PauseBeforeCommandInterceptor("INSERT INTO \"CaveChangeRequestStagedFiles\"");
        await using var proposalDb = database.CreateDbContext("contributor", cave.AccountId, pause);
        var createTask = new CaveChangeRequestRepository(proposalDb, proposalDb.RequestUser)
            .CreateAsync(cave.CaveId, cave.RevisionId, proposal, default);
        await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));

        var untilExpired = expiresOn - DateTime.UtcNow + TimeSpan.FromMilliseconds(100);
        if (untilExpired > TimeSpan.Zero)
            await Task.Delay(untilExpired);

        await using var cleanupDb = database.CreateDbContext("cleanup", cave.AccountId);
        await cleanupDb.Database.OpenConnectionAsync();
        var cleanupPid = await PostgresLockAssertions.BackendPidAsync(cleanupDb);
        var cleanupTask = new AuthoringFileStore(cleanupDb, cleanupDb.RequestUser, TimeProvider.System)
            .RemoveExpiredAsync(16, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, cleanupPid, cleanupTask);

        pause.Continue();
        var requestId = await createTask;
        var cleanupTargets = await cleanupTask;

        Assert.Empty(cleanupTargets);
        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.True(await verify.Files.AnyAsync(file => file.Id == fileId));
        Assert.True(await verify.CaveChangeRequestStagedFiles.AnyAsync(link =>
            link.AccountId == cave.AccountId && link.ChangeRequestId == requestId && link.FileId == fileId));
        Assert.Empty(await verify.StorageCleanupTargets.Where(target =>
            target.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval).ToListAsync());
        Assert.Single(blobs.Keys);
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
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        var blobs = new TestFileBlobStore();
        string requestId;
        string versionWithFileId;
        string stagedFileId;

        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, tenant.AccountId, "contributor", blobs))
        {
            var requests = new CaveChangeRequestRepository(contributor.Db, contributor.Db.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Historical file"), default);

            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("historical attachment"));
            var staged = await contributor.Services.StageRequestFileForTestAsync(
                requestId, content, "Lost survey attachment.pdf", null, default);
            stagedFileId = staged.Id;
            versionWithFileId = await CurrentVersionAsync(contributor.Db, requestId);

            await CavePermissions.AuthenticateAsync(contributor.Db, tenant.AccountId);
            await contributor.ChangeRequests.AddVersionAsync(requestId,
                PublishableValues(tenant, locationTag.Id, "Historical file"), false,
                tenant.RevisionId, versionWithFileId, default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            await IntegrationTestServices.For(reviewer, blobs).CaveChangeRequests.RejectAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), "No attachment needed", default);
        }

        await using var audit = database.CreateDbContext("contributor", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(audit, tenant.AccountId);
        var historical = await IntegrationTestServices.For(audit, blobs).CaveChangeRequests
            .GetVersionAsync(requestId, versionWithFileId, default);
        var unavailable = Assert.Single(historical.Proposed.Files,
            file => file.Id == stagedFileId);
        Assert.Equal("Lost survey attachment", unavailable.Name);
        Assert.Equal(".pdf", unavailable.Extension);
        Assert.Contains(stagedFileId, historical.UnavailableStagedFileIds);
        Assert.False(await audit.Files.AnyAsync(file => file.Id == stagedFileId));
    }

    [Fact]
    public async Task RejectionLocksFileUntilUnreferencedDeletionCommitsBeforeAnotherRequestCanClaim()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RejectionLocksFileUntilUnreferencedDeletionCommitsBeforeAnotherRequestCanClaim));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        var blobs = new TestFileBlobStore();

        string rejectedRequestId;
        string otherRequestId;
        string fileId;
        await using (var author = await CaveTestActor.CreateAsync(
                         database, cave.AccountId, "contributor", blobs))
        {
            var requests = new CaveChangeRequestRepository(author.Db, author.Db.RequestUser);
            rejectedRequestId = await requests.CreateAsync(
                cave.CaveId, cave.RevisionId, Proposal(cave, "First proposal"), default);
            otherRequestId = await requests.CreateAsync(
                cave.CaveId, cave.RevisionId, Proposal(cave, "Second proposal"), default);
            await using var content = new MemoryStream("file payload"u8.ToArray());
            var staged = await author.Services.StageRequestFileForTestAsync(
                rejectedRequestId, content, "staged.pdf", null, default);
            fileId = staged.Id;
        }

        var pause = new PauseBeforeCommandInterceptor("INSERT INTO \"StorageCleanupTargets\"");
        await using var rejectDb = database.CreateDbContext("reviewer", cave.AccountId, pause);
        var rejectedVersionId = await CurrentVersionAsync(rejectDb, rejectedRequestId);
        var rejectionTask = new CaveChangeRequestRepository(rejectDb, rejectDb.RequestUser)
            .RejectAsync(rejectedRequestId, rejectedVersionId, "Not suitable", default);
        try
        {
            await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));

            await using var claimDb = database.CreateDbContext("contributor", cave.AccountId);
            await claimDb.Database.OpenConnectionAsync();
            var claimPid = await PostgresLockAssertions.BackendPidAsync(claimDb);
            var claimTask = Task.Run(async () =>
            {
                await using var transaction = await claimDb.Database.BeginTransactionAsync();
                await new AuthoringFileStore(claimDb, claimDb.RequestUser, TimeProvider.System)
                    .ClaimAsync(otherRequestId, [fileId], default);
                await claimDb.SaveChangesAsync();
                await transaction.CommitAsync();
            });

            // Claim must wait for rejection's File row lock. Otherwise it can
            // create a new FK reference after unreferenced-file discovery.
            await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, claimPid, claimTask);
            pause.Continue();

            await rejectionTask;
            var claimFailure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => claimTask);
            Assert.Equal(404, claimFailure.StatusCode);
        }
        finally
        {
            pause.Continue();
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.False(await verify.Files.AnyAsync(file => file.Id == fileId));
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(staged => staged.FileId == fileId));
        Assert.Equal(CaveChangeRequestStatus.Rejected,
            await verify.CaveChangeRequests.Where(request => request.Id == rejectedRequestId)
                .Select(request => request.Status).SingleAsync());
        Assert.Single(await verify.StorageCleanupTargets.Where(target =>
            target.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval).ToListAsync());
    }

    private sealed class PauseBeforeCommandInterceptor(string fragment) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _continue =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Reached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Continue() => _continue.TrySetResult();

        private async Task PauseIfMatchAsync(DbCommand command, CancellationToken cancellationToken)
        {
            if (!command.CommandText.Contains(fragment, StringComparison.Ordinal)) return;
            Reached.TrySetResult();
            await _continue.Task.WaitAsync(cancellationToken);
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await PauseIfMatchAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await PauseIfMatchAsync(command, cancellationToken);
            return result;
        }
    }

}
