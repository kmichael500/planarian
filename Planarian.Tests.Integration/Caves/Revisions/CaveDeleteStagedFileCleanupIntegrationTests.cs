using Microsoft.EntityFrameworkCore;
using Planarian.Modules.Caves.Repositories;
using Xunit;

namespace Planarian.Tests;

public sealed class CaveDeleteStagedFileCleanupIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task StagedFileCleanupRemovesOnlyCurrentAccountReferences()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StagedFileCleanupRemovesOnlyCurrentAccountReferences));
        var accountA = await TestDataScenarios.CreatePublishedCaveScenarioAsync(database, 'a');
        var accountB = await TestDataScenarios.CreatePublishedCaveScenarioAsync(database, 'b');

        await using (var db = database.CreateDbContext("manager", accountA.AccountId))
        {
            var repository = new CaveRepository(db, db.RequestUser);
            await repository.DeleteStagedFileReferencesAsync([accountA.FileId, accountB.FileId]);
        }

        await using (var verifyA = database.CreateDbContext("manager", accountA.AccountId))
        {
            Assert.False(await verifyA.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
                .AnyAsync(row => row.AccountId == accountA.AccountId && row.Id == accountA.StagedFileId));
        }

        await using (var verifyB = database.CreateDbContext("manager", accountB.AccountId))
        {
            Assert.True(await verifyB.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
                .AnyAsync(row => row.AccountId == accountB.AccountId && row.Id == accountB.StagedFileId));
        }
    }
}
