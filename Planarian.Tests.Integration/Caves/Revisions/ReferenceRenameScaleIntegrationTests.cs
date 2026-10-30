using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Caves.Revisions;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using Planarian.Operations.CaveRevisionHistory;

using Planarian.Tests;

namespace Planarian.Tests.Integration.Caves.Revisions;

[Collection(ScaleIntegrationCollection.Name)]
public sealed class ReferenceRenameScaleIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    private const int CaveCount = 10_000;
    private const string AccountId = "renameac01";
    private const string StateId = "renamest01";
    private const string CountyId = "renamecn01";

    [Fact]
    public async Task TenThousandCaveCountyRenameRemainsBoundedAndPublishesExactlyOneRevisionPerCave()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenThousandCaveCountyRenameRemainsBoundedAndPublishesExactlyOneRevisionPerCave));
        await SeedTenThousandLegacyCavesAsync(database);

        await using (var rollout = database.CreateDbContext("rollout-scale", AccountId))
        {
            var result = await new CaveRevisionRolloutRepository(rollout).InitializeAsync();
            Assert.Equal(CaveCount, result.InitializedCaveCount);
        }

        var counter = new DbCommandCounter();
        await using (var rename = database.CreateDbContext("manager", AccountId, counter))
        {
            counter.Reset();
            await IntegrationTestServices.For(rename).Account.CreateOrUpdateCounty(
                StateId,
                new CreateCountyVm { Name = "Renamed County", CountyDisplayId = "R99" },
                CountyId, default);

            Assert.InRange(counter.Commands.Count, 1, 500);
            Assert.Empty(rename.ChangeTracker.Entries());
        }

        await using var verify = database.CreateDbContext("verify", AccountId);
        Assert.Equal(CaveCount * 2, await verify.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(revision => revision.AccountId == AccountId));
        Assert.Equal(CaveCount, await verify.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(revision => revision.AccountId == AccountId &&
                                    revision.Source == CaveRevisionSource.ManagerEdit &&
                                    revision.Operation == CaveRevisionOperation.Update &&
                                    revision.PreviousRevisionId != null));
        Assert.Equal(CaveCount, await verify.Caves.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(cave => cave.AccountId == AccountId && cave.CurrentRevisionId != null));

        var first = await verify.Caves.IgnoreQueryFilters().AsNoTracking()
            .Where(cave => cave.AccountId == AccountId)
            .OrderBy(cave => cave.Id)
            .Select(cave => cave.CurrentRevisionId!)
            .FirstAsync();
        var snapshot = CaveSnapshotJson.Deserialize(
            (await verify.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(revision => revision.Id == first)).SnapshotJson, 1);
        Assert.Equal(CountyId, snapshot.County.Id);
        Assert.Equal("Renamed County", snapshot.County.NameAtRevision);
        Assert.Equal("R99", snapshot.County.DisplayIdAtRevision);
    }

    private static async Task SeedTenThousandLegacyCavesAsync(PostgresTestDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into "States"("Id","Name","Abbreviation","CreatedOn")
            values(@state_id,'Tennessee','TN',now());

            insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn")
            values(@account_id,'Reference Rename Scale','-',false,true,now());

            insert into "Counties"("Id","AccountId","StateId","DisplayId","Name","CreatedOn")
            values(@county_id,@account_id,@state_id,'A01','Original County',now());

            insert into "Caves"(
                "Id","AccountId","StateId","CountyId","Name","AlternateNames","CountyNumber","IsArchived","CreatedOn")
            select 'c' || lpad(series::text, 9, '0'), @account_id, @state_id, @county_id,
                   'Scale Cave ' || series, '[]', series, false, now()
            from generate_series(1, 10000) as series;
            """;
        command.Parameters.AddWithValue("state_id", StateId);
        command.Parameters.AddWithValue("account_id", AccountId);
        command.Parameters.AddWithValue("county_id", CountyId);
        await command.ExecuteNonQueryAsync();
    }
}
