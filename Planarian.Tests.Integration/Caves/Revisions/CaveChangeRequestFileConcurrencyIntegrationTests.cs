using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Tags.Repositories;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveChangeRequestFileConcurrencyIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task AggregateFileMetadataReplacementLocksOldAndNewFileTypesBeforeWaitingMerge()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AggregateFileMetadataReplacementLocksOldAndNewFileTypesBeforeWaitingMerge));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var file = await FileTestDataFactory.AddFileAsync(database, cave, associateWithCave: true,
            fileId: "replace00a");
        var replacement = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.File, "Replacement", "filetype0b");
        var mergeDestination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.File, "Merge Destination", "filetype0c");
        await CavePermissions.GrantManagerAsync(database, cave, "writer");
        string beforeRevision;
        await using (var seed = database.CreateDbContext("publish-file", cave.AccountId))
        {
            var mutations = new CaveMutationRepository(seed, seed.RequestUser,
                new CavePublishedSnapshotRepository(seed, seed.RequestUser));
            beforeRevision = (await mutations.PublishExistingAsync(cave.CaveId, cave.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, _ => { })).RevisionId!;
        }

        var hold = new HoldAcquiredTagReferenceLockInterceptor();
        await using var writerDb = database.CreateDbContext("writer", cave.AccountId, hold);
        await CavePermissions.EnsureAccountUserAsync(writerDb, cave.AccountId);
        await writerDb.RequestUser.Initialize(cave.AccountId, writerDb.RequestUser.Id);
        var writerCave = await new CaveRepository(writerDb, writerDb.RequestUser).GetCave(cave.CaveId);
        var updateValues = ValuesFromCave(writerCave!);
        updateValues.Files = [new EditFileMetadataVm
        {
            Id = file.FileId, FileTypeTagId = replacement.Id, DisplayName = "Replacement file"
        }];
        var update = IntegrationTestServices.For(writerDb).Caves.AddCave(updateValues, default);
        await hold.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Contains(file.FileTypeId, hold.LockedIds);
        Assert.Contains(replacement.Id, hold.LockedIds);

        await using var mergeDb = database.CreateDbContext("manager", cave.AccountId);
        await mergeDb.Database.OpenConnectionAsync();
        int mergePid;
        await using (var command = mergeDb.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "select pg_backend_pid()";
            mergePid = Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        var merge = new TagTypeMergeExecutionRepository(mergeDb, mergeDb.RequestUser,
            new TagReferenceLockRepository(mergeDb, mergeDb.RequestUser),
            new CavePublishedSnapshotRepository(mergeDb, mergeDb.RequestUser),
            new CaveBulkRevisionRepository(mergeDb, mergeDb.RequestUser))
            .ExecuteAsync([file.FileTypeId], mergeDestination.Id);
        await WaitForLockWaitAsync(database, cave.AccountId, mergePid);

        hold.Resume.TrySetResult();
        await update;
        await merge;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(replacement.Id, (await verify.Files.SingleAsync(row => row.Id == file.FileId)).FileTypeTagId);
        var pointer = await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync();
        var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == pointer);
        Assert.Equal(beforeRevision, revision.PreviousRevisionId);
        Assert.Equal(replacement.Id, Assert.Single(CaveSnapshotJson.Deserialize(revision.SnapshotJson, 1).Files)
            .FileTypeTagId);
        Assert.False(await verify.CaveRevisions.AnyAsync(row => row.PreviousRevisionId == pointer));
    }

    private static async Task WaitForLockWaitAsync(PostgresTestDatabase database, string accountId, int backendPid)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var observer = database.CreateDbContext("file-lock-observer", accountId);
            await observer.Database.OpenConnectionAsync();
            await using var command = observer.Database.GetDbConnection().CreateCommand();
            command.CommandText = "select \"wait_event_type\" from pg_stat_activity where pid = @pid";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "pid";
            parameter.Value = backendPid;
            command.Parameters.Add(parameter);
            if (string.Equals(await command.ExecuteScalarAsync() as string, "Lock", StringComparison.Ordinal)) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Tag merge did not reach the expected File Type lock wait.");
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
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", System.Text.Encoding.UTF8.GetBytes("concurrency bytes"));
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

        string acceptedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            acceptedRevisionId = (await IntegrationTestServices.For(reviewer, blobs).CaveChangeRequests.ApproveAsync(requestId,
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
            var current = await new CaveRepository(manager, manager.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(current!);
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId,
                FileTypeTagId = laterType.Id,
                DisplayName = "Manager follow-up photograph"
            }];
            await IntegrationTestServices.For(manager).Caves.AddCave(values, default);
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
}
