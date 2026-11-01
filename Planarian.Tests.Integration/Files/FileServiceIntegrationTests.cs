using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Services;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

using Planarian.Modules.Files.Persistence;
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
    public async Task ExpiredUnpublishedCleanupClaimsOnlyOneBoundedBatch()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ExpiredUnpublishedCleanupClaimsOnlyOneBoundedBatch));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var other = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, FileTypeTagName.Other, "fileotherb");

        await using (var seed = database.CreateDbContext("seed", tenant.AccountId))
        {
            seed.Files.AddRange(Enumerable.Range(0, 17).Select(index =>
            {
                var id = $"exp{index:0000000}";
                return new Planarian.Model.Database.Entities.RidgeWalker.File
                {
                    Id = id,
                    AccountId = tenant.AccountId,
                    FileTypeTagId = other.Id,
                    Name = $"expired-{index}",
                    Extension = ".csv",
                    BlobContainer = RequestUser.BuildAccountContainerName(tenant.AccountId),
                    BlobKey = AuthoringFileStoragePath.BuildKey(id),
                    ExpiresOn = DateTime.UtcNow.AddDays(-1)
                };
            }));
            await seed.SaveChangesAsync();
        }

        await using (var cleanup = database.CreateDbContext("cleanup", tenant.AccountId))
            await new Planarian.Modules.Files.Persistence.AuthoringFileStore(cleanup, cleanup.RequestUser, TimeProvider.System)
                .RemoveExpiredAsync(16, default);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var remaining = await verify.Files.AsNoTracking()
            .Where(file => file.CaveId == null && file.ExpiresOn != null)
            .OrderBy(file => file.Id)
            .Select(file => file.Id)
            .ToListAsync();

        Assert.Equal(["exp0000016"], remaining);
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
        var expiredContainer = RequestUser.BuildAccountContainerName(tenant.AccountId);
        var expiredKey = AuthoringFileStoragePath.BuildKey(expiredId);
        await using (var seed = database.CreateDbContext("seed", tenant.AccountId))
        {
            seed.Files.Add(new Planarian.Model.Database.Entities.RidgeWalker.File
            {
                Id = expiredId, AccountId = tenant.AccountId, FileTypeTagId = other.Id,
                Name = "expired", Extension = ".csv", BlobContainer = expiredContainer, BlobKey = expiredKey,
                ExpiresOn = DateTime.UtcNow.AddMinutes(-1)
            });
            await seed.SaveChangesAsync();
        }

        var blobs = new TestFileBlobStore { FailWrites = true };
        blobs.Seed(expiredContainer, expiredKey, [1, 2, 3]);
        await using var actor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "uploader", blobs);

        await Assert.ThrowsAsync<IOException>(() => actor.Services.Files.StageAuthoringFile(
            new MemoryStream([4, 5]), "new.csv", default));

        Assert.False(blobs.Contains(expiredContainer, expiredKey));
        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.False(await verify.Files.IgnoreQueryFilters().AnyAsync(file => file.Id == expiredId));
    }
}
