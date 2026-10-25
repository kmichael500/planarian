using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
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
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            $"{nameof(EntranceOtherBackfillRollbackPreservesEveryPreexistingOverride)}_{useGeneratedIdCollision}");
        var completion = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CaveRevisionsCompletionMigrationSuffix, StringComparison.Ordinal));
        await database.MigrateAsync(completion);
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');

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
            command.Parameters.AddWithValue("account", account.AccountId);
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
            command.Parameters.AddWithValue("account", account.AccountId);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.False(reader.GetBoolean(0));
            Assert.False(reader.GetBoolean(1));
            Assert.True(reader.GetBoolean(2));
        }

        await database.MigrateAsync(completion);

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        var setting = await verify.FeatureSettings.SingleAsync(row =>
            row.AccountId == account.AccountId &&
            row.Key == FeatureKey.EnabledFieldEntranceOtherTags);
        Assert.False(setting.IsEnabled);
    }

    [Fact]
    public async Task EntranceOtherBackfillTracksAndRollsBackOnlyRowsItCreated()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(EntranceOtherBackfillTracksAndRollsBackOnlyRowsItCreated));
        var completion = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CaveRevisionsCompletionMigrationSuffix, StringComparison.Ordinal));
        await database.MigrateAsync(completion);
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');

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
                where setting."AccountId"=@account
                  and setting."Key"='EnabledFieldEntranceOtherTags'
                """, afterUp);
            afterUpCommand.Parameters.AddWithValue("account", account.AccountId);
            await using var afterUpReader = await afterUpCommand.ExecuteReaderAsync();
            Assert.True(await afterUpReader.ReadAsync());
            Assert.True(afterUpReader.GetBoolean(0));
            Assert.True(afterUpReader.GetBoolean(1));
            Assert.False(afterUpReader.GetBoolean(2));
        }

        await database.MigrateAsync(completion);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select
              not exists (
                select 1 from "FeatureSettings"
                where "AccountId"=@account and "Key"='EnabledFieldEntranceOtherTags'),
              to_regclass('public."__BackfillEntranceOtherFeatureSettingRows"') is null
            """, verify);
        command.Parameters.AddWithValue("account", account.AccountId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
    }
}
