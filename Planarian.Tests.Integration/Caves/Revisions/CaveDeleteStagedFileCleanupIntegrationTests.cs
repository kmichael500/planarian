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
        var accountA = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var accountB = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');
        var reviewA = await TestDataBuilder.CreatePendingReviewWithStagedFileAsync(database, accountA);
        var reviewB = await TestDataBuilder.CreatePendingReviewWithStagedFileAsync(database, accountB);

        await using (var db = database.CreateDbContext("manager", accountA.AccountId))
        {
            var repository = new CaveRepository(db, db.RequestUser);
            await repository.DeleteStagedFileReferencesAsync([reviewA.FileId, reviewB.FileId]);
        }

        await using (var verifyA = database.CreateDbContext("manager", accountA.AccountId))
        {
            Assert.False(await verifyA.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
                .AnyAsync(row => row.AccountId == accountA.AccountId && row.Id == reviewA.StagedFileId));
        }

        await using (var verifyB = database.CreateDbContext("manager", accountB.AccountId))
        {
            Assert.True(await verifyB.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
                .AnyAsync(row => row.AccountId == accountB.AccountId && row.Id == reviewB.StagedFileId));
        }
    }
}
