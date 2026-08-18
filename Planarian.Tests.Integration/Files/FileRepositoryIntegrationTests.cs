using Planarian.Modules.Files.Repositories;
using Planarian.Tests;
using Xunit;

namespace Planarian.Tests.Integration.Files;

public sealed class FileRepositoryIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Theory]
    [InlineData("seed-a", ".pdf", true)]
    [InlineData("seed-a", ".PDF", false)]
    [InlineData("seed-a", ".jpg", false)]
    [InlineData("different", ".pdf", false)]
    public async Task DuplicateFileMatchUsesExactNameAndExtensionPair(
        string name, string extension, bool expected)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(DuplicateFileMatchUsesExactNameAndExtensionPair)}_{name}_{extension}_{expected}");
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "duplicate1a");

        await using var db = database.CreateDbContext("file-query", tenant.AccountId);
        var repository = new FileRepository(db, db.RequestUser);

        var isDuplicate = await repository.IsDuplicateFile(tenant.CaveId, name, extension);

        Assert.Equal(expected, isDuplicate);
    }
}
