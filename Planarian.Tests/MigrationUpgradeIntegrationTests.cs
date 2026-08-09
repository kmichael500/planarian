using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Planarian.Tests;

public sealed class MigrationUpgradeIntegrationTests(PostgresIntegrationFixture fixture) : IClassFixture<PostgresIntegrationFixture>
{
    private const string MainBaselineMigration = "20260423021136_v29";
    [Fact]
    public async Task EmptyDatabaseMigratesToLatest()
    {
        await using var database=await fixture.CreateUnmigratedDatabaseAsync(nameof(EmptyDatabaseMigratesToLatest)); await database.MigrateAsync(null);
        await using var connection=new NpgsqlConnection(database.ConnectionString); await connection.OpenAsync();
        await using var command=new NpgsqlCommand("select exists(select 1 from information_schema.tables where table_name='CaveRevisions'),exists(select 1 from information_schema.tables where table_name='CaveChangeRequests'),postgis_version()",connection);
        await using var r=await command.ExecuteReaderAsync(); Assert.True(await r.ReadAsync()); Assert.True(r.GetBoolean(0)); Assert.True(r.GetBoolean(1)); Assert.False(string.IsNullOrWhiteSpace(r.GetString(2)));
    }

    [Fact]
    public async Task ExactMainSchemaUpgradesWithoutCorruptingExistingTenantData()
    {
        await using var database=await fixture.CreateUnmigratedDatabaseAsync(nameof(ExactMainSchemaUpgradesWithoutCorruptingExistingTenantData));
        var migrations=database.GetMigrationNames().ToList();
        var baseline=migrations.FindIndex(m=>m.EndsWith(MainBaselineMigration,StringComparison.Ordinal));
        var foundation=migrations.FindIndex(m=>m.EndsWith("_CaveRevisionImportFoundation",StringComparison.Ordinal));
        Assert.True(baseline >= 0, $"Expected main baseline migration {MainBaselineMigration} was not found.");
        Assert.Equal(baseline + 1, foundation);
        await database.MigrateAsync(migrations[baseline]);
        await using(var connection=new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync(); await using var c=connection.CreateCommand(); c.CommandText="""
            insert into "States"("Id","Name","Abbreviation","CreatedOn") values('mainstate1','Tennessee','TN',now());
            insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn") values('mainacct01','Existing Main Account','-',false,true,now());
            insert into "Counties"("Id","AccountId","StateId","DisplayId","Name","CreatedOn") values('maincnty01','mainacct01','mainstate1','MAIN','Existing County',now());
            insert into "Caves"("Id","AccountId","StateId","CountyId","Name","AlternateNames","CountyNumber","IsArchived","CreatedOn") values('maincave01','mainacct01','mainstate1','maincnty01','Existing Main Cave','[]',42,false,now());
            insert into "TagTypes"("Id","AccountId","Key","Name","IsDefault","CreatedOn") values('mainfile01','mainacct01','file','Existing file',false,now());
            insert into "Files"("Id","AccountId","CaveId","FileTypeTagId","FileName","BlobKey","BlobContainer","CreatedOn") values
              ('mainfiler1','mainacct01','maincave01','mainfile01','existing-cave.pdf','caves/maincave01/files/mainfiler1.pdf','main',now()),
              ('mainfilet1','mainacct01',null,'mainfile01','temporary.csv','temp/import/caves/mainfilet1.csv','main',now());
            """; await c.ExecuteNonQueryAsync();
        }
        await database.MigrateAsync(null);
        await using(var verify=database.CreateDbContext("main","mainacct01"))
        {
            var cave=await verify.Caves.IgnoreQueryFilters().SingleAsync(c=>c.Id=="maincave01"); Assert.Equal("Existing Main Cave",cave.Name); Assert.Equal("mainacct01",cave.AccountId); Assert.Null(cave.CurrentRevisionId);
            var files=await verify.Files.OrderBy(f=>f.Id).ToListAsync(); Assert.Equal(2,files.Count);
            Assert.Equal(("mainacct01","maincave01","existing-cave.pdf"),(files[0].AccountId,files[0].CaveId,files[0].FileName));
            Assert.Equal(("mainacct01",(string?)null,"temporary.csv"),(files[1].AccountId,files[1].CaveId,files[1].FileName));
        }
        await using var a=database.CreateDbContext("a","mainacct01"); await using var b=database.CreateDbContext("b","mainacct01");
        var ca=await a.Caves.IgnoreQueryFilters().SingleAsync(c=>c.Id=="maincave01"); var cb=await b.Caves.IgnoreQueryFilters().SingleAsync(c=>c.Id=="maincave01"); var version=ca.Version;
        ca.Name="Updated after upgrade"; await a.SaveChangesAsync(); Assert.NotEqual(version,ca.Version); cb.Name="Stale"; await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>b.SaveChangesAsync());
    }

    [Fact]
    public async Task V29LegacyCaveFileWithNullOwnerIsBackfilledFromItsCave()
    {
        await using var database = await CreateV29DatabaseAsync(nameof(V29LegacyCaveFileWithNullOwnerIsBackfilledFromItsCave));
        await SeedV29FileAsync(database, "legfile01", accountId: null, caveId: "legcave01",
            fileName: "legacy-cave.pdf", blobKey: "caves/legcave01/files/legfile01.pdf");

        await database.MigrateAsync(null);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select f."AccountId", f."CaveId", f."FileName", f."BlobKey",
                   exists(select 1 from pg_constraint where conname = 'FK_Files_Accounts_AccountId'),
                   exists(select 1 from pg_constraint where conname = 'AK_Files_AccountId_Id'),
                   exists(select 1 from pg_constraint where conname = 'FK_CaveChangeRequestStagedFiles_Files_AccountId_FileId')
            from "Files" f where f."Id" = 'legfile01'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("legacyacct", reader.GetString(0));
        Assert.Equal("legcave01", reader.GetString(1));
        Assert.Equal("legacy-cave.pdf", reader.GetString(2));
        Assert.Equal("caves/legcave01/files/legfile01.pdf", reader.GetString(3));
        Assert.True(reader.GetBoolean(4));
        Assert.True(reader.GetBoolean(5));
        Assert.True(reader.GetBoolean(6));
    }

    [Fact]
    public async Task V29TemporaryAccountFileSurvivesOwnershipMigrationUnchanged()
    {
        await using var database = await CreateV29DatabaseAsync(nameof(V29TemporaryAccountFileSurvivesOwnershipMigrationUnchanged));
        await SeedV29FileAsync(database, "temporary1", "legacyacct", null, "temporary.csv", "temp/import/temporary1.csv");

        await database.MigrateAsync(null);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select \"AccountId\", \"CaveId\", \"FileName\", \"BlobKey\" from \"Files\" where \"Id\"='temporary1'", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("legacyacct", reader.GetString(0));
        Assert.True(reader.IsDBNull(1));
        Assert.Equal("temporary.csv", reader.GetString(2));
        Assert.Equal("temp/import/temporary1.csv", reader.GetString(3));
    }

    [Fact]
    public async Task V29UnassignableNullOwnerFileFailsClosedAndLeavesV29Schema()
    {
        await using var database = await CreateV29DatabaseAsync(nameof(V29UnassignableNullOwnerFileFailsClosedAndLeavesV29Schema));
        await SeedV29FileAsync(database, "orphanfile", null, null, "orphan.pdf", "orphan/orphanfile.pdf");

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.MigrateAsync(null));
        Assert.Contains("Cannot migrate Files.AccountId", error.MessageText, StringComparison.Ordinal);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select \"AccountId\" is null, is_nullable from \"Files\" join information_schema.columns on table_name='Files' and column_name='AccountId' where \"Id\"='orphanfile'", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.Equal("YES", reader.GetString(1));
    }

    private async Task<PostgresTestDatabase> CreateV29DatabaseAsync(string name)
    {
        var database = await fixture.CreateUnmigratedDatabaseAsync(name);
        var migration = database.GetMigrationNames().Single(x => x.EndsWith(MainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(migration);
        return database;
    }

    private static async Task SeedV29FileAsync(PostgresTestDatabase database, string fileId, string? accountId, string? caveId,
        string fileName, string blobKey)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            insert into "States"("Id","Name","Abbreviation","CreatedOn") values('legstate','Legacy State','LS',now()) on conflict do nothing;
            insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn") values('legacyacct','Legacy Account','-',false,true,now()) on conflict do nothing;
            insert into "Counties"("Id","AccountId","StateId","DisplayId","Name","CreatedOn") values('legcounty','legacyacct','legstate','LEG','Legacy County',now()) on conflict do nothing;
            insert into "Caves"("Id","AccountId","StateId","CountyId","Name","AlternateNames","CountyNumber","IsArchived","CreatedOn") values('legcave01','legacyacct','legstate','legcounty','Legacy Cave','[]',1,false,now()) on conflict do nothing;
            insert into "TagTypes"("Id","AccountId","Key","Name","IsDefault","CreatedOn") values('legacyfile','legacyacct','file','Legacy file',false,now()) on conflict do nothing;
            insert into "Files"("Id","AccountId","CaveId","FileTypeTagId","FileName","BlobKey","BlobContainer","CreatedOn") values(@id,@account,@cave,'legacyfile',@name,@blob,'legacy',now())
            """, connection);
        command.Parameters.AddWithValue("id", fileId);
        command.Parameters.AddWithValue("account", (object?)accountId ?? DBNull.Value);
        command.Parameters.AddWithValue("cave", (object?)caveId ?? DBNull.Value);
        command.Parameters.AddWithValue("name", fileName);
        command.Parameters.AddWithValue("blob", blobKey);
        await command.ExecuteNonQueryAsync();
    }
}
