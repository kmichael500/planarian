using Microsoft.EntityFrameworkCore;
using Planarian.Modules.Caves.Repositories;
using Xunit;

using Planarian.Tests;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveDeleteStagedFileCleanupIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task StagedFileCleanupRemovesOnlyCurrentAccountReferences()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StagedFileCleanupRemovesOnlyCurrentAccountReferences));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var reviewA = await CaveChangeRequestTestDataFactory.CreatePendingReviewWithStagedFileAsync(database, accountA);
        var reviewB = await CaveChangeRequestTestDataFactory.CreatePendingReviewWithStagedFileAsync(database, accountB);

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
