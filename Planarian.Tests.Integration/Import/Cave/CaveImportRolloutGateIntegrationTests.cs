using Microsoft.EntityFrameworkCore;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class CaveImportRolloutGateIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ExistingCaveWithoutCurrentRevisionFailsClosedForSyncUpdate()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ExistingCaveWithoutCurrentRevisionFailsClosedForSyncUpdate));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var cave = await CaveTestDataFactory.AddCaveAsync(database, account);

        await using var db = database.CreateDbContext("import", account.AccountId);
        var import = new CaveImportTestHarness(db, db.RequestUser);
        var plan = await import.PlanCsvAsync(
            CaveCsv($"Updated,{account.CountyName},{account.CountyDisplayId},{cave.CountyNumber}," +
                    $"{account.StateAbbreviation},,,,,100,,,,,,,,,,false,,"),
            syncExisting: true);
        Assert.Equal(CaveImportAction.Update, Assert.Single(plan.Caves).Action);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            import.ExecuteAsync(plan, "rollout-update.csv"));

        Assert.Contains("rollout must complete", error.Message, StringComparison.OrdinalIgnoreCase);
        db.ChangeTracker.Clear();
        var persisted = await db.Caves.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal(cave.CaveName, persisted.Name);
        Assert.Null(persisted.CurrentRevisionId);
        Assert.False(await db.CaveImportBatches.AnyAsync(row => row.SourceFileName == "rollout-update.csv"));
        Assert.Empty(await db.CaveRevisions.Where(row => row.CaveId == cave.CaveId).ToListAsync());
    }

    [Fact]
    public async Task ExistingCaveWithoutCurrentRevisionFailsClosedForSyncDeletion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ExistingCaveWithoutCurrentRevisionFailsClosedForSyncDeletion));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var cave = await CaveTestDataFactory.AddCaveAsync(database, account);

        await using var db = database.CreateDbContext("import", account.AccountId);
        var import = new CaveImportTestHarness(db, db.RequestUser);
        var plan = await import.PlanCsvAsync(
            ImportDryRunIntegrationTests.CaveHeader + "\n",
            syncExisting: true);
        Assert.Equal(cave.CaveId, Assert.Single(plan.Deletions).CaveId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            import.ExecuteAsync(plan, "rollout-delete.csv"));

        Assert.Contains("rollout must complete", error.Message, StringComparison.OrdinalIgnoreCase);
        db.ChangeTracker.Clear();
        var persisted = await db.Caves.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == cave.CaveId);
        Assert.Null(persisted.CurrentRevisionId);
        Assert.False(await db.CaveImportBatches.AnyAsync(row => row.SourceFileName == "rollout-delete.csv"));
        Assert.Empty(await db.CaveRevisions.Where(row => row.CaveId == cave.CaveId).ToListAsync());
    }

    private static string CaveCsv(string row) =>
        ImportDryRunIntegrationTests.CaveHeader + "\n" + row + "\n";
}
