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
}
