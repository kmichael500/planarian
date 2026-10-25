using Npgsql;
using Xunit;
using static Planarian.Tests.MigrationTestSupport;

namespace Planarian.Tests;

[Collection(MigrationIntegrationCollection.Name)]
public sealed class FileOwnershipMigrationIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task CurrentMainV32MismatchedCaveFileFailsClosedAndRollsBackEarlierOwnershipRepair()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(CurrentMainV32MismatchedCaveFileFailsClosedAndRollsBackEarlierOwnershipRepair));

        await using (var seed = new NpgsqlConnection(database.ConnectionString))
        {
            await seed.OpenAsync();
            await using var seedCommand = new NpgsqlCommand("""
                insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn")
                values('otheracct1','Other Account','-',false,true,now());

                update "Files" set "AccountId" = null where "Id" = 'mainfiler1';

                insert into "Files"("Id","AccountId","CaveId","FileTypeTagId","FileName","DisplayName","BlobKey","BlobContainer","CreatedOn")
                values('wrongfile1','otheracct1','maincave01','mainfile01','wrong-owner.pdf','wrong-owner',
                       'wrong-owner','main',now());
                """, seed);
            await seedCommand.ExecuteNonQueryAsync();
        }

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.MigrateAsync(null));
        Assert.Contains("does not match its owning Cave account", error.MessageText, StringComparison.Ordinal);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select
              (select "AccountId" is null from "Files" where "Id"='mainfiler1'),
              (select "AccountId" from "Files" where "Id"='wrongfile1'),
              exists(select 1 from information_schema.columns where table_name='Files' and column_name='DisplayName'),
              not exists(select 1 from information_schema.columns where table_name='Files' and column_name='Name'),
              (select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId" desc limit 1)
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.Equal("otheracct1", reader.GetString(1));
        Assert.True(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
        Assert.Equal(CurrentMainBaselineMigration, reader.GetString(4));
    }

    [Fact]
    public async Task CurrentMainV32CaveFileWithNullOwnerIsBackfilledFromItsCave()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(CurrentMainV32CaveFileWithNullOwnerIsBackfilledFromItsCave));

        await using (var seed = new NpgsqlConnection(database.ConnectionString))
        {
            await seed.OpenAsync();
            await using var seedCommand = new NpgsqlCommand(
                """update "Files" set "AccountId" = null where "Id" = 'mainfiler1'""", seed);
            await seedCommand.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select f."AccountId", f."CaveId", f."Name", f."Extension", f."BlobKey",
                   exists(select 1 from pg_constraint where conname = 'FK_Files_Accounts_AccountId'),
                   exists(select 1 from pg_constraint where conname = 'AK_Files_AccountId_Id'),
                   exists(select 1 from pg_constraint where conname = 'FK_CaveChangeRequestStagedFiles_Files_AccountId_FileId')
            from "Files" f where f."Id" = 'mainfiler1'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("mainacct01", reader.GetString(0));
        Assert.Equal("maincave01", reader.GetString(1));
        Assert.Equal("existing-cave", reader.GetString(2));
        Assert.Equal(".pdf", reader.GetString(3));
        Assert.Equal("caves/maincave01/files/mainfiler1.pdf", reader.GetString(4));
        Assert.True(reader.GetBoolean(5));
        Assert.True(reader.GetBoolean(6));
        Assert.True(reader.GetBoolean(7));
    }

    [Fact]
    public async Task CurrentMainV32AccountOwnedFileWithoutCaveSurvivesOwnershipMigrationUnchanged()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(CurrentMainV32AccountOwnedFileWithoutCaveSurvivesOwnershipMigrationUnchanged));

        await database.MigrateAsync(null);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "AccountId", "CaveId", "Name", "Extension", "BlobKey"
            from "Files" where "Id"='mainfilet1'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("mainacct01", reader.GetString(0));
        Assert.True(reader.IsDBNull(1));
        Assert.Equal("temporary", reader.GetString(2));
        Assert.Equal(".csv", reader.GetString(3));
        Assert.Equal("temp/import/caves/mainfilet1.csv", reader.GetString(4));
    }

    [Fact]
    public async Task CurrentMainV32UnassignableNullOwnerFileFailsClosedAndLeavesV32Schema()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(CurrentMainV32UnassignableNullOwnerFileFailsClosedAndLeavesV32Schema));

        await using (var seed = new NpgsqlConnection(database.ConnectionString))
        {
            await seed.OpenAsync();
            await using var seedCommand = new NpgsqlCommand("""
                update "Files" set "AccountId" = null, "CaveId" = null where "Id"='mainfiler1'
                """, seed);
            await seedCommand.ExecuteNonQueryAsync();
        }

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.MigrateAsync(null));
        Assert.Contains("Cannot migrate Files.AccountId", error.MessageText, StringComparison.Ordinal);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "AccountId" is null, is_nullable,
                   (select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId" desc limit 1)
            from "Files"
            join information_schema.columns on table_name='Files' and column_name='AccountId'
            where "Id"='mainfiler1'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.Equal("YES", reader.GetString(1));
        Assert.Equal(CurrentMainBaselineMigration, reader.GetString(2));
    }
}
