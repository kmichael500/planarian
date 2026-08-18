using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Controllers;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveAggregateAssetIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    private const string EmptyFeatureCollection = "{\"type\":\"FeatureCollection\",\"features\":[]}";

    [Fact]
    public async Task StaleDirectManagerEditCannotOverwriteANewerRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(StaleDirectManagerEditCannotOverwriteANewerRevision));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager-a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager-b");

        var first = PublishableValues(tenant, quality.Id, "First manager wins");
        var stale = PublishableValues(tenant, quality.Id, "Stale manager loses");

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager-a"))
            await manager.Services.Caves.AddCave(first, default);

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager-b"))
        {
            var conflict = await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
                manager.Services.Caves.AddCave(stale, default));
            Assert.Equal(tenant.RevisionId, conflict.ExpectedRevisionId);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal("First manager wins", cave.Name);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
    }

    [Fact]
    public async Task DirectLinePlotSaveUsesStableIdentityAndCanonicalNoOpDoesNotAddARevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectLinePlotSaveUsesStableIdentityAndCanonicalNoOpDoesNotAddARevision));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        string publishedRevisionId;
        string linePlotId;
        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var context = await manager.Services.Caves.GetEditAuthoringContextAsync(tenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.LinePlots = [new() { Name = "Survey line", GeoJson = EmptyFeatureCollection }];
            await manager.Services.Caves.AddCave(values, default);

            var savedContext = await manager.Services.Caves.GetEditAuthoringContextAsync(tenant.CaveId, default);
            publishedRevisionId = savedContext.Cave.CurrentRevisionId!;
            var savedLinePlot = Assert.Single(savedContext.LinePlots);
            linePlotId = savedLinePlot.Id!;

            var noOp = ValuesFromCave(savedContext.Cave);
            noOp.LinePlots = [new()
            {
                Id = linePlotId,
                Name = "Survey line",
                GeoJson = "{ \"features\" : [ ], \"type\" : \"FeatureCollection\" }"
            }];
            await manager.Services.Caves.AddCave(noOp, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal(publishedRevisionId, cave.CurrentRevisionId);
        var linePlot = await verify.CaveGeoJsons.SingleAsync(row => row.CaveId == tenant.CaveId);
        Assert.Equal(linePlotId, linePlot.Id);
        Assert.Equal("{\"features\":[],\"type\":\"FeatureCollection\"}", CaveJsonContent.Normalize(linePlot.GeoJson));
        var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == publishedRevisionId);
        var snapshotLinePlot = Assert.Single(CaveSnapshotJson.Deserialize(revision.SnapshotJson, 1).LinePlots);
        Assert.Equal(linePlotId, snapshotLinePlot.Id);
    }

    [Fact]
    public async Task PublishedFileRemovalRetainsProviderNeutralAddressUntilHardDelete()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PublishedFileRemovalRetainsProviderNeutralAddressUntilHardDelete));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true, fileId: "retain000a");
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", System.Text.Encoding.UTF8.GetBytes("historical file bytes"));
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        string revisionWithFile;
        await using (var seed = database.CreateDbContext("file-revision-seed", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(seed, seed.RequestUser,
                new CavePublishedSnapshotRepository(seed, seed.RequestUser));
            revisionWithFile = (await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, _ => { })).RevisionId!;
        }

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs))
        {
            var cave = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            Assert.Equal(revisionWithFile, values.ExpectedRevisionId);
            values.Files = [];
            await manager.Services.Caves.AddCave(values, default);
        }

        await using (var retained = database.CreateDbContext("verify-retained", tenant.AccountId))
        {
            Assert.False(await retained.Files.AnyAsync(row => row.Id == file.FileId));
            var address = await retained.RetainedCaveFileObjects.SingleAsync(row => row.FileId == file.FileId);
            Assert.Equal((tenant.AccountId, tenant.CaveId, "test", "seed-a"),
                (address.AccountId, address.CaveId, address.StoragePartition, address.StorageKey));
            Assert.True(blobs.Contains("test", "seed-a"));
        }

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs))
            await manager.Services.Caves.DeleteCave(tenant.CaveId, default);

        await using var verify = database.CreateDbContext("verify-delete", tenant.AccountId);
        Assert.False(await verify.RetainedCaveFileObjects.AnyAsync(row => row.FileId == file.FileId));
        Assert.False(blobs.Contains("test", "seed-a"));
    }

    [Fact]
    public async Task GenericAuthoringFilePublishesWithoutCopyingItsObject()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(GenericAuthoringFilePublishesWithoutCopyingItsObject));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        var blobs = new TestFileBlobStore();

        await using var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs);
        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("one upload"));
        var staged = await manager.Services.Files.StageAuthoringFile(content, "survey.pdf", default);
        var before = await manager.Db.Files.AsNoTracking().SingleAsync(row => row.Id == staged.Id);
        Assert.Equal(("survey", ".pdf"), (before.Name, before.Extension));
        Assert.Null(before.CaveId);
        Assert.NotNull(before.ExpiresOn);
        var objectAddress = (before.BlobContainer!, before.BlobKey!);
        Assert.Equal($"objects/files/{staged.Id}", before.BlobKey);
        Assert.Single(blobs.Keys);

        var cave = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
        var values = ValuesFromCave(cave!);
        values.Files = [new EditFileMetadataVm
        {
            Id = staged.Id,
            FileTypeTagId = staged.FileTypeTagId,
            Name = staged.Name!
        }];
        await manager.Services.Caves.AddCave(values, default);
        manager.Db.ChangeTracker.Clear();

        var published = await manager.Db.Files.AsNoTracking().SingleAsync(row => row.Id == staged.Id);
        Assert.Equal(tenant.CaveId, published.CaveId);
        Assert.Null(published.ExpiresOn);
        Assert.Equal(objectAddress, (published.BlobContainer!, published.BlobKey!));
        Assert.Single(blobs.Keys);
        Assert.True(blobs.Contains(objectAddress.Item1, objectAddress.Item2));
    }

    [Fact]
    public async Task DirectFileRenameUpdatesNameAndPreservesExtensionFileTypeAndStorageIdentity()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectFileRenameUpdatesNameAndPreservesExtensionFileTypeAndStorageIdentity));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "rename000a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        await using (var seed = database.CreateDbContext("file-rename-baseline", tenant.AccountId))
        {
            var mutation = await new CaveMutationRepository(seed, seed.RequestUser,
                    new CavePublishedSnapshotRepository(seed, seed.RequestUser))
                .PublishExistingAsync(tenant.CaveId, tenant.RevisionId, CaveRevisionSource.ManagerEdit,
                    CaveRevisionOperation.Update, _ => { });
            tenant = tenant with { RevisionId = mutation.RevisionId! };
        }

        string originalPartition;
        string originalKey;
        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var original = await manager.Db.Files.AsNoTracking().SingleAsync(row => row.Id == file.FileId);
            originalPartition = original.BlobContainer!;
            originalKey = original.BlobKey!;
            var cave = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId,
                Name = "Entrance Survey",
                FileTypeTagId = original.FileTypeTagId
            }];
            await manager.Services.Caves.AddCave(values, default);
        }

        await using var verify = database.CreateDbContext("file-rename-verify", tenant.AccountId);
        var currentCave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        var currentFile = await verify.Files.AsNoTracking().SingleAsync(row => row.Id == file.FileId);
        Assert.Equal("Entrance Survey", currentFile.Name);
        Assert.Equal(".pdf", currentFile.Extension);
        Assert.Equal(file.FileId, currentFile.Id);
        Assert.Equal((originalPartition, originalKey), (currentFile.BlobContainer, currentFile.BlobKey));
        Assert.Equal(file.FileTypeId, currentFile.FileTypeTagId);

        var previous = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == tenant.RevisionId)).SnapshotJson, 1);
        var current = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == currentCave.CurrentRevisionId)).SnapshotJson, 1);
        var fileChange = Assert.Single(new CaveRevisionDiffService().Compare(previous, current).FileChanges);
        var scalar = Assert.Single(fileChange.Scalars);
        Assert.Equal(nameof(CaveFileSnapshotV1.Name), scalar.Key);
        Assert.Equal(("seed-a", "Entrance Survey"), (scalar.Value.Previous, scalar.Value.Current));
    }

    [Fact]
    public async Task DirectFileRenameRejectsANameThatNewlyIncludesItsFixedExtension()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectFileRenameRejectsANameThatNewlyIncludesItsFixedExtension));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "badname00a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        await using (var seed = database.CreateDbContext("file-name-validation-baseline", tenant.AccountId))
        {
            var mutation = await new CaveMutationRepository(seed, seed.RequestUser,
                    new CavePublishedSnapshotRepository(seed, seed.RequestUser))
                .PublishExistingAsync(tenant.CaveId, tenant.RevisionId, CaveRevisionSource.ManagerEdit,
                    CaveRevisionOperation.Update, _ => { });
            tenant = tenant with { RevisionId = mutation.RevisionId! };
        }

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var cave = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId,
                Name = "seed-a.pdf",
                FileTypeTagId = file.FileTypeId
            }];

            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                manager.Services.Caves.AddCave(values, default));
            Assert.Equal(400, failure.StatusCode);
            Assert.Contains("must not include its fixed extension", failure.Message, StringComparison.Ordinal);
        }

        await using var verify = database.CreateDbContext("file-name-validation-verify", tenant.AccountId);
        var unchangedFile = await verify.Files.AsNoTracking().SingleAsync(row => row.Id == file.FileId);
        Assert.Equal(("seed-a", ".pdf"), (unchangedFile.Name, unchangedFile.Extension));
        Assert.Equal(tenant.RevisionId, (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == tenant.CaveId)).CurrentRevisionId);
    }

    [Fact]
    public async Task DirectPublicationRejectsMissingStagedObjectBeforeMutatingCave()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectPublicationRejectsMissingStagedObjectBeforeMutatingCave));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        var blobs = new TestFileBlobStore();

        await using var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager", blobs);
        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("missing after staging"));
        var staged = await manager.Services.Files.StageAuthoringFile(content, "missing.pdf", default);
        var stagedRow = await manager.Db.Files.AsNoTracking().SingleAsync(row => row.Id == staged.Id);
        blobs.Delete(stagedRow.BlobContainer!, stagedRow.BlobKey!);
        var revisionCountBefore = await manager.Db.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId);

        var cave = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
        var values = ValuesFromCave(cave!);
        values.Name = "Must not publish";
        values.Files = [new EditFileMetadataVm
        {
            Id = staged.Id,
            FileTypeTagId = staged.FileTypeTagId,
            Name = staged.Name!
        }];

        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            manager.Services.Caves.AddCave(values, default));
        Assert.Contains("no longer available", failure.Message);

        manager.Db.ChangeTracker.Clear();
        var unchanged = await manager.Db.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal(tenant.CaveName, unchanged.Name);
        Assert.Equal(tenant.RevisionId, unchanged.CurrentRevisionId);
        var stillStaged = await manager.Db.Files.AsNoTracking().SingleAsync(row => row.Id == staged.Id);
        Assert.Null(stillStaged.CaveId);
        Assert.NotNull(stillStaged.ExpiresOn);
        Assert.Equal(revisionCountBefore,
            await manager.Db.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
    }

    [Fact]
    public async Task InitialProposalCannotClaimExpiredGenericStagedFile()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InitialProposalCannotClaimExpiredGenericStagedFile));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var location = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        var blobs = new TestFileBlobStore();

        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId,
            "contributor", blobs);
        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("expired staging"));
        var staged = await contributor.Services.Files.StageAuthoringFile(content, "expired.pdf", default);
        var stagedRow = await contributor.Db.Files.SingleAsync(row => row.Id == staged.Id);
        stagedRow.ExpiresOn = DateTime.UtcNow.AddMinutes(-1);
        await contributor.Db.SaveChangesAsync();

        var values = PublishableValues(tenant, location.Id, "Expired generic staging");
        values.Files = [new EditFileMetadataVm
        {
            Id = staged.Id,
            FileTypeTagId = staged.FileTypeTagId,
            Name = staged.Name!
        }];

        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.CreateAsync(tenant.CaveId, values, tenant.RevisionId, default));

        contributor.Db.ChangeTracker.Clear();
        Assert.False(await contributor.Db.CaveChangeRequests.AnyAsync(row => row.CaveId == tenant.CaveId));
        Assert.False(await contributor.Db.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == staged.Id));
        Assert.NotNull((await contributor.Db.Files.SingleAsync(row => row.Id == staged.Id)).ExpiresOn);
    }

    [Fact]
    public async Task InitialProposalClaimsGenericStagedFileOnlyWhenProposalIsCreated()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InitialProposalClaimsGenericStagedFileOnlyWhenProposalIsCreated));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File,
            "Other", "other0000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        var blobs = new TestFileBlobStore();

        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor", blobs);
        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("proposal bytes"));
        var staged = await contributor.Services.Files.StageAuthoringFile(content, "proposal.pdf", default);
        Assert.False(await contributor.Db.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == staged.Id));

        var context = await contributor.ChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
        var values = ValuesFromCave(context.Cave);
        values.Name = "Proposal with staged file";
        values.Files = [new EditFileMetadataVm
        {
            Id = staged.Id,
            FileTypeTagId = staged.FileTypeTagId,
            Name = staged.Name!
        }];
        var requestId = await contributor.ChangeRequests.CreateAsync(
            tenant.CaveId, values, context.ExpectedBaseRevisionId, default);

        Assert.True(await contributor.Db.CaveChangeRequestStagedFiles.AnyAsync(row =>
            row.ChangeRequestId == requestId && row.FileId == staged.Id));
        var file = await contributor.Db.Files.AsNoTracking().SingleAsync(row => row.Id == staged.Id);
        Assert.Null(file.CaveId);
        Assert.NotNull(file.ExpiresOn);
        Assert.Single(blobs.Keys);
    }
    [Fact]
    public async Task GenericStagedFileCannotBeReadOrDeletedByAnotherAccountUser()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(GenericStagedFileCannotBeReadOrDeletedByAnotherAccountUser));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var fileType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Other", "other0000a");
        var blobs = new TestFileBlobStore();
        string fileId;
        string partition;
        string key;

        await using (var owner = await CaveTestActor.CreateAsync(database, tenant.AccountId, "owner", blobs))
        {
            await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("private staged bytes"));
            var staged = await owner.Services.Files.StageAuthoringFile(content, "private.pdf", default);
            Assert.Equal(fileType.Id, staged.FileTypeTagId);
            var row = await owner.Db.Files.AsNoTracking().SingleAsync(file => file.Id == staged.Id);
            fileId = row.Id;
            partition = row.BlobContainer!;
            key = row.BlobKey!;
        }

        await using (var other = await CaveTestActor.CreateAsync(database, tenant.AccountId, "other", blobs))
        {
            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                other.Services.Files.OpenAuthoringStagedFileAsync(fileId, default));
            Assert.Equal(404, failure.StatusCode);
            Assert.Contains("Staged file", failure.Message, StringComparison.Ordinal);
            await other.Services.Files.DeleteUnpublishedFileAsync(fileId, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.True(await verify.Files.AnyAsync(file => file.Id == fileId && file.CaveId == null));
        Assert.True(blobs.Contains(partition, key));
    }

    [Fact]
    public async Task ProposalVersionKeepsExactLinePlotPayloadAfterPublishedCaveChanges()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalVersionKeepsExactLinePlotPayloadAfterPublishedCaveChanges));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        const string proposedGeoJson =
            "{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"properties\":{\"source\":\"proposal\"},\"geometry\":null}]}";

        string requestId;
        string versionId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var context = await contributor.ChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.Name = "Proposal owns exact line plot";
            values.LinePlots = [new() { Name = "Proposal line", GeoJson = proposedGeoJson }];
            requestId = await contributor.ChangeRequests.CreateAsync(
                tenant.CaveId, values, context.ExpectedBaseRevisionId, default);
            versionId = await CurrentVersionAsync(contributor.Db, requestId);
        }

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var context = await manager.Services.Caves.GetEditAuthoringContextAsync(tenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.LinePlots = [new()
            {
                Name = "Different published line",
                GeoJson = "{\"type\":\"FeatureCollection\",\"features\":[]}"
            }];
            await manager.Services.Caves.AddCave(values, default);
        }

        await using var reader = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");
        var version = await reader.ChangeRequests.GetVersionAsync(requestId, versionId, default);
        var linePlot = Assert.Single(version.LinePlots);
        Assert.Equal("Proposal line", linePlot.Name);
        Assert.Equal(CaveJsonContent.NormalizeFeatureCollection(proposedGeoJson), linePlot.GeoJson);
    }

}
