using Microsoft.EntityFrameworkCore;
using Planarian.Modules.Account.Repositories;
using Xunit;

namespace Planarian.Tests.Integration.AccountArchive;

public sealed class ArchiveFileProjectionIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ArchiveProjectsNameAndExtensionAsTheCompleteExternalFileName()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ArchiveProjectsNameAndExtensionAsTheCompleteExternalFileName));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "archive01a");

        await using var db = database.CreateDbContext("archive", tenant.AccountId);
        var entity = await db.Files.SingleAsync(row => row.Id == file.FileId);
        entity.Name = "Mügelhöhle.final";
        entity.Extension = ".PDF";
        await db.SaveChangesAsync();

        var result = Assert.Single(await new AccountRepository(db, db.RequestUser)
            .GetArchiveFiles(tenant.AccountId, default));

        Assert.Equal("Mügelhöhle.final.PDF", result.FileName);
        Assert.Equal(file.FileId, result.Id);
        Assert.Equal(entity.BlobKey, result.BlobKey);
    }
}
