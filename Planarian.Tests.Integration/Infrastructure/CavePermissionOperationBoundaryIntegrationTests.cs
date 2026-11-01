using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.Integration.Infrastructure;

public sealed class CavePermissionOperationBoundaryIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Theory]
    [InlineData(PermissionKey.View, "fileview01")]
    [InlineData(PermissionKey.Manager, "filemgr001")]
    public async Task CaveScopedPermissionCannotReadAnotherCavesFileInTheSameState(
        string grantedPermission, string userId)
    {
        await using var database =
            await fixture.CreateDatabaseAsync($"file_scope_{grantedPermission.ToLowerInvariant()}");
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var sibling = await CaveTestDataFactory.AddCaveAsync(
            database, source, "cave00000b", "Sibling Cave", 2);
        var siblingPublished = await CaveTestDataFactory.PublishBaselineRevisionAsync(
            database, sibling, "revision0b");
        var file = await FileTestDataFactory.AddFileAsync(
            database, siblingPublished, associateWithCave: true, fileId: "file00000b");

        if (grantedPermission == PermissionKey.View)
            await CavePermissions.GrantViewAsync(database, source, userId);
        else
            await CavePermissions.GrantManagerAsync(database, source, userId);

        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", [1, 2, 3]);

        await using var actor = await CaveTestActor.CreateAsync(database, source.AccountId, userId, blobs);

        await Assert.ThrowsAsync<ApiException>(() =>
            actor.Services.Files.CreateFileResponse(file.FileId, isDownload: false, default));
    }

    [Fact]
    public async Task CaveScopedManagerCannotUseAdminImportSourceWorkflowButAccountAdminCan()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveScopedManagerCannotUseAdminImportSourceWorkflowButAccountAdminCan));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string sourceId = "impsourcea";
        const string managerId = "scopedmgr1";
        const string adminId = "accountadm1";
        var bytes = new byte[] { 1, 4, 9, 16 };

        string container;
        string key;
        await using (var seed = database.CreateDbContext("seed", cave.AccountId))
        {
            container = seed.RequestUser.AccountContainerName;
            key = ImportSourceStoragePath.BuildCaveEntranceCsvKey(sourceId);
            seed.CaveImportSources.Add(new CaveImportSource
            {
                Id = sourceId,
                AccountId = cave.AccountId,
                FileName = "retained-source.csv",
                BlobContainer = container,
                BlobKey = key
            });
            seed.CaveImportBatches.Add(new CaveImportBatch
            {
                Id = "impbatchia",
                AccountId = cave.AccountId,
                SourceFileId = sourceId,
                SourceFileName = "retained-source.csv",
                Kind = CaveImportKind.CaveCsv,
                SourceRecordCount = 1
            });
            await seed.SaveChangesAsync();
        }

        var blobs = new TestFileBlobStore();
        blobs.Seed(container, key, bytes);

        await CavePermissions.GrantManagerAsync(database, cave, managerId);
        await using (var manager = await CaveTestActor.CreateAsync(
                         database, cave.AccountId, managerId, blobs))
        {
            var denied = await Assert.ThrowsAsync<ApiException>(() =>
                manager.Db.RequestUser.HasCavePermission(PermissionPolicyKey.Admin));
            Assert.Equal(403, denied.StatusCode);

            var hiddenFromGenericFiles = await Assert.ThrowsAsync<ApiException>(() =>
                manager.Services.Files.CreateFileResponse(sourceId, isDownload: true, default));
            Assert.Equal(404, hiddenFromGenericFiles.StatusCode);
        }

        await CavePermissionMatrixTestSupport.GrantAccountAdminAsync(database, cave.AccountId, adminId);
        await using var admin = await CaveTestActor.CreateAsync(database, cave.AccountId, adminId, blobs);
        Assert.True(await admin.Db.RequestUser.HasCavePermission(PermissionPolicyKey.Admin));

        var hiddenFromGenericAdminFiles = await Assert.ThrowsAsync<ApiException>(() =>
            admin.Services.Files.CreateFileResponse(sourceId, isDownload: true, default));
        Assert.Equal(404, hiddenFromGenericAdminFiles.StatusCode);

        var response = await admin.Services.Imports.OpenCsvImportSourceDownloadAsync(sourceId, default);
        Assert.Equal("retained-source.csv", response.FileName);
        await using var stream = response.Stream;
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        Assert.Equal(bytes, memory.ToArray());
    }

    [Fact]
    public async Task AccountAdminCannotReadImportSourceFromAnotherAccountsStorageContainer()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AccountAdminCannotReadImportSourceFromAnotherAccountsStorageContainer));
        var own = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var foreign = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        const string sourceId = "impsourcec";
        const string adminId = "accountadm2";
        var foreignContainer = RequestUser.BuildAccountContainerName(foreign.AccountId);
        var key = ImportSourceStoragePath.BuildCaveEntranceCsvKey(sourceId);
        var bytes = new byte[] { 8, 6, 7, 5, 3, 0, 9 };

        await using (var seed = database.CreateDbContext("seed", own.AccountId))
        {
            seed.CaveImportSources.Add(new CaveImportSource
            {
                Id = sourceId,
                AccountId = own.AccountId,
                FileName = "wrong-container.csv",
                BlobContainer = foreignContainer,
                BlobKey = key
            });
            seed.CaveImportBatches.Add(new CaveImportBatch
            {
                Id = "impbatchic",
                AccountId = own.AccountId,
                SourceFileId = sourceId,
                SourceFileName = "wrong-container.csv",
                Kind = CaveImportKind.CaveCsv,
                SourceRecordCount = 1
            });
            await seed.SaveChangesAsync();
        }

        var blobs = new TestFileBlobStore();
        blobs.Seed(foreignContainer, key, bytes);
        await CavePermissionMatrixTestSupport.GrantAccountAdminAsync(database, own.AccountId, adminId);

        await using var admin = await CaveTestActor.CreateAsync(database, own.AccountId, adminId, blobs);
        Assert.True(await admin.Db.RequestUser.HasCavePermission(PermissionPolicyKey.Admin));
        var denied = await Assert.ThrowsAsync<ApiException>(() =>
            admin.Services.Imports.OpenCsvImportSourceDownloadAsync(sourceId, default));

        Assert.Equal(400, denied.StatusCode);
        Assert.True(blobs.Contains(foreignContainer, key));
    }

    [Fact]
    public async Task AccountAdminCannotUseGenericFileEndpointToBypassTemporaryAuthoringOwnership()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AccountAdminCannotUseGenericFileEndpointToBypassTemporaryAuthoringOwnership));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.File, "Other", "filetypeib");
        const string ownerId = "stageownr1";
        const string adminId = "stageadm01";
        var blobs = new TestFileBlobStore();

        await CavePermissions.GrantViewAsync(database, cave, ownerId);
        string fileId;
        await using (var owner = await CaveTestActor.CreateAsync(database, cave.AccountId, ownerId, blobs))
        {
            await using var content = new MemoryStream(new byte[] { 2, 3, 5, 7 });
            var staged = await owner.Services.Files.StageAuthoringFile(content, "private-stage.pdf", default);
            fileId = staged.Id;

            var opened = await owner.Services.Files.OpenAuthoringStagedFileAsync(fileId, default);
            await opened.Stream.DisposeAsync();
        }

        await CavePermissionMatrixTestSupport.GrantAccountAdminAsync(database, cave.AccountId, adminId);
        await using var admin = await CaveTestActor.CreateAsync(database, cave.AccountId, adminId, blobs);
        var denied = await Assert.ThrowsAsync<ApiException>(() =>
            admin.Services.Files.CreateFileResponse(fileId, isDownload: true, default));
        Assert.Equal(404, denied.StatusCode);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.True(await verify.Files.AnyAsync(file => file.Id == fileId && file.ExpiresOn != null));
    }

    [Fact]
    public async Task CaveManagerCountyNumberHelpersRequireExistingCaveContext()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(CaveManagerCountyNumberHelpersRequireExistingCaveContext));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string userId = "helpermgr1";

        await CavePermissions.GrantManagerAsync(database, cave, userId);
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, userId);

        var nextFailure = await Assert.ThrowsAsync<ApiException>(() =>
            actor.Services.Caves.GetNextCountyNumber(cave.CountyId));
        Assert.Equal(403, nextFailure.StatusCode);

        var inUseFailure = await Assert.ThrowsAsync<ApiException>(() =>
            actor.Services.Caves.IsCountyNumberInUse(cave.CountyId, cave.CountyNumber));
        Assert.Equal(403, inUseFailure.StatusCode);

        Assert.True(await actor.Services.Caves.GetNextCountyNumber(
            cave.CountyId, caveId: cave.CaveId) > cave.CountyNumber);
        Assert.False(await actor.Services.Caves.IsCountyNumberInUse(
            cave.CountyId, cave.CountyNumber, cave.CaveId));
    }

    [Fact]
    public async Task CountyManagerCanUseCountyNumberHelpersForNewCaveCreation()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(CountyManagerCanUseCountyNumberHelpersForNewCaveCreation));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string userId = "countymgr1";

        await CavePermissions.GrantCountyManagerAsync(database, cave, userId);
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, userId);

        Assert.True(await actor.Services.Caves.GetNextCountyNumber(cave.CountyId) > cave.CountyNumber);
        Assert.True(await actor.Services.Caves.IsCountyNumberInUse(cave.CountyId, cave.CountyNumber));
    }
}
