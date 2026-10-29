using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
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
