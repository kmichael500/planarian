using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Generators;
using Xunit;

namespace Planarian.Tests;

public sealed class DataGeneratorIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task GeneratedDataFailureIsSurfacedAndTransactionRollsBack()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(GeneratedDataFailureIsSurfacedAndTransactionRollsBack));
        await using var db = CreateSeedDbContext(database.ConnectionString);
        var before = await GetCounts(db);

        await db.Database.ExecuteSqlRawAsync("""
            alter table "States"
            add constraint "CK_Test_RejectTennessee"
            check ("Abbreviation" <> 'TN')
            """);

        var generator = new DataGenerator(db);
        await Assert.ThrowsAsync<DbUpdateException>(() => generator.AddOrUpdateDefaultData());

        await using var verifyDb = database.CreateDbContext("generator-verify", null);
        Assert.Equal(before, await GetCounts(verifyDb));
    }

    private static PlanarianDbContext CreateSeedDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseNetTopologySuite())
            .Options;
        return new TestSeedDbContext(options);
    }

    private static async Task<GeneratedDataCounts> GetCounts(PlanarianDbContext db) => new(
        await db.States.CountAsync(),
        await db.Permissions.CountAsync(),
        await db.TagTypes.CountAsync(),
        await db.FeatureSettings.CountAsync());

    // DataGenerator writes global reference data. Deliberately suppress the normal
    // Planarian SaveChangesInterceptor so this test exercises PostgreSQL transaction
    // rollback rather than application authorization.
    private sealed class TestSeedDbContext(DbContextOptions<PlanarianDbContext> options)
        : PlanarianDbContext(options)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }
    }

    private sealed record GeneratedDataCounts(
        int States,
        int Permissions,
        int Tags,
        int FeatureSettings);
}
