using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Services;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

namespace Planarian.Tests.Integration.Files;

public sealed class FileServiceIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task PublishedFileResponseComposesCompleteNameAndUsesStoredExtensionForContentPolicy()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PublishedFileResponseComposesCompleteNameAndUsesStoredExtensionForContentPolicy));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "response0a", name: "Mügelhöhle.final", extension: ".pdf");
        await CavePermissions.GrantViewAsync(database, tenant, "viewer");
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", [1, 2, 3]);

        await using var viewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "viewer", blobs);
        var response = await viewer.Services.Files.CreateFileResponse(file.FileId, isDownload: false, default);

        Assert.Equal("Mügelhöhle.final.pdf", response.FileName);
        Assert.Equal("application/pdf", response.ContentType);
        Assert.False(response.Download);
    }
    [Fact]
    public async Task FailedTemporaryUploadDoesNotOrphanExpiredObjectsWhoseRowsWereCleaned()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(FailedTemporaryUploadDoesNotOrphanExpiredObjectsWhoseRowsWereCleaned));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var other = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, FileTypeTagName.Other, "fileothera");
        const string expiredId = "expired01a";
        const string expiredKey = "expired-object";
        await using (var seed = database.CreateDbContext("seed", tenant.AccountId))
        {
            seed.Files.Add(new Planarian.Model.Database.Entities.RidgeWalker.File
            {
                Id = expiredId, AccountId = tenant.AccountId, FileTypeTagId = other.Id,
                Name = "expired", Extension = ".csv", BlobContainer = "test", BlobKey = expiredKey,
                ExpiresOn = DateTime.UtcNow.AddMinutes(-1)
            });
            await seed.SaveChangesAsync();
        }

        var blobs = new TestFileBlobStore { FailWrites = true };
        blobs.Seed("test", expiredKey, [1, 2, 3]);
        await using var actor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "uploader", blobs);

        await Assert.ThrowsAsync<IOException>(() => actor.Services.Files.AddTemporaryAccountFile(
            new MemoryStream([4, 5]), "new.csv", FileTypeTagName.Other, default));

        Assert.False(blobs.Contains("test", expiredKey));
        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.False(await verify.Files.IgnoreQueryFilters().AnyAsync(file => file.Id == expiredId));
    }
}
