using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Caves.Revisions;
using Xunit;

namespace Planarian.Tests.Integration.Caves.Revisions;

[Collection(ScaleIntegrationCollection.Name)]
public sealed class CaveRevisionRolloutScaleIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    private const int CaveCount = 10_000;

    [Fact]
    public async Task TenThousandCaveRolloutIsBoundedAndCreatesOneBaselinePerCave()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenThousandCaveRolloutIsBoundedAndCreatesOneBaselinePerCave));
        const string accountId = "rollacct01";
        await SeedTenThousandLegacyCavesAsync(database, accountId);

        await using var db = database.CreateDbContext("rollout-scale", accountId);
        var result = await new CaveRevisionRolloutRepository(db).InitializeAsync();

        Assert.Equal(CaveCount, result.InitializedCaveCount);
        Assert.Equal(4, result.BatchCount);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(CaveCount, await db.Caves.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(cave => cave.AccountId == accountId && cave.CurrentRevisionId != null));
        Assert.Equal(CaveCount, await db.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(revision => revision.AccountId == accountId));
        Assert.Equal(0, await db.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(revision => revision.AccountId == accountId &&
                                    (revision.Source != CaveRevisionSource.SystemBaseline ||
                                     revision.Operation != CaveRevisionOperation.Baseline ||
                                     revision.PreviousRevisionId != null ||
                                     revision.CreatedByUserId != null)));
        Assert.Equal(1, await db.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .Where(revision => revision.AccountId == accountId)
            .Select(revision => revision.CreatedOn)
            .Distinct()
            .CountAsync());
    }

    private static async Task SeedTenThousandLegacyCavesAsync(PostgresTestDatabase database, string accountId)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into "States"("Id","Name","Abbreviation","CreatedOn")
            values('rollstate1','Tennessee','TN',now());

            insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn")
            values(@account_id,'Revision Rollout Scale','-',false,true,now());

            insert into "Counties"("Id","AccountId","StateId","DisplayId","Name","CreatedOn")
            values('rollcnty01',@account_id,'rollstate1','ROLL','Rollout County',now());

            insert into "Caves"(
                "Id","AccountId","StateId","CountyId","Name","AlternateNames","CountyNumber","IsArchived","CreatedOn")
            select 'c' || lpad(series::text, 9, '0'), @account_id, 'rollstate1', 'rollcnty01',
                   'Legacy Cave ' || series, '[]', series, false, now()
            from generate_series(1, 10000) as series;
            """;
        command.Parameters.AddWithValue("account_id", accountId);
        await command.ExecuteNonQueryAsync();
    }
}
