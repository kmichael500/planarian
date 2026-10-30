using Npgsql;
using Xunit;
using static Planarian.Tests.MigrationTestSupport;

namespace Planarian.Tests;

[Collection(MigrationIntegrationCollection.Name)]
public sealed class FeatureSettingMigrationIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EntranceOtherBackfillRollbackPreservesEveryPreexistingOverride(
        bool useGeneratedIdCollision)
    {
        // The v32 upgrade must preserve an existing
        // override created before the upgrade.
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, $"{nameof(EntranceOtherBackfillRollbackPreservesEveryPreexistingOverride)}_{useGeneratedIdCollision}");

        await using (var seed = new NpgsqlConnection(database.ConnectionString))
        {
            await seed.OpenAsync();
            await using var command = new NpgsqlCommand("""
                insert into "FeatureSettings" ("Id","AccountId","Key","IsEnabled","IsDefault","CreatedOn")
                values (
                    case when @collide
                         then substr(md5(@account || ':EnabledFieldEntranceOtherTags'), 1, 10)
                         else 'manualothr'
                    end,
                    @account, 'EnabledFieldEntranceOtherTags', false, false, now())
                """, seed);
            command.Parameters.AddWithValue("collide", useGeneratedIdCollision);
            command.Parameters.AddWithValue("account", "mainacct01");
            await command.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using (var afterUp = new NpgsqlConnection(database.ConnectionString))
        {
            await afterUp.OpenAsync();
            await using var command = new NpgsqlCommand("""
                select setting."IsEnabled", setting."IsDefault",
                       not exists (
                           select 1 from "__BackfillEntranceOtherFeatureSettingRows" inserted
                           where inserted."AccountId" = setting."AccountId")
                from "FeatureSettings" setting
                where setting."AccountId"=@account
                  and setting."Key"='EnabledFieldEntranceOtherTags'
                """, afterUp);
            command.Parameters.AddWithValue("account", "mainacct01");
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.False(reader.GetBoolean(0));
            Assert.False(reader.GetBoolean(1));
            Assert.True(reader.GetBoolean(2));
        }

        await database.MigrateAsync(CurrentMainBaselineMigration);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var commandAfterDown = new NpgsqlCommand("""
            select "IsEnabled", "IsDefault"
            from "FeatureSettings"
            where "AccountId"='mainacct01' and "Key"='EnabledFieldEntranceOtherTags'
            """, verify);
        await using var restored = await commandAfterDown.ExecuteReaderAsync();
        Assert.True(await restored.ReadAsync());
        Assert.False(restored.GetBoolean(0));
        Assert.False(restored.GetBoolean(1));
    }

    [Fact]
    public async Task EntranceOtherBackfillTracksAndRollsBackOnlyRowsItCreated()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(EntranceOtherBackfillTracksAndRollsBackOnlyRowsItCreated));

        await database.MigrateAsync(null);

        await using (var afterUp = new NpgsqlConnection(database.ConnectionString))
        {
            await afterUp.OpenAsync();
            await using var afterUpCommand = new NpgsqlCommand("""
                select setting."Id" = inserted."FeatureSettingId",
                       setting."IsEnabled", setting."IsDefault"
                from "FeatureSettings" setting
                join "__BackfillEntranceOtherFeatureSettingRows" inserted
                  on inserted."AccountId" = setting."AccountId"
                where setting."AccountId"='mainacct01'
                  and setting."Key"='EnabledFieldEntranceOtherTags'
                """, afterUp);
            await using var afterUpReader = await afterUpCommand.ExecuteReaderAsync();
            Assert.True(await afterUpReader.ReadAsync());
            Assert.True(afterUpReader.GetBoolean(0));
            Assert.True(afterUpReader.GetBoolean(1));
            Assert.False(afterUpReader.GetBoolean(2));
        }

        await database.MigrateAsync(CurrentMainBaselineMigration);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select
              not exists (
                select 1 from "FeatureSettings"
                where "AccountId"='mainacct01' and "Key"='EnabledFieldEntranceOtherTags'),
              to_regclass('public."__BackfillEntranceOtherFeatureSettingRows"') is null
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
    }
}
