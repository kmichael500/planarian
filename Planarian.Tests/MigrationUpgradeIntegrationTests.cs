using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Planarian.Tests;

public sealed class MigrationUpgradeIntegrationTests(PostgresIntegrationFixture fixture) : IClassFixture<PostgresIntegrationFixture>
{
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
        var migrations=database.GetMigrationNames().ToList(); var foundation=migrations.FindIndex(m=>m.EndsWith("_CaveRevisionImportFoundation",StringComparison.Ordinal)); Assert.True(foundation>0);
        await database.MigrateAsync(migrations[foundation-1]);
        await using(var connection=new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync(); await using var c=connection.CreateCommand(); c.CommandText="""
            insert into "States"("Id","Name","Abbreviation","CreatedOn") values('mainstate1','Tennessee','TN',now());
            insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn") values('mainacct01','Existing Main Account','-',false,true,now());
            insert into "Counties"("Id","AccountId","StateId","DisplayId","Name","CreatedOn") values('maincnty01','mainacct01','mainstate1','MAIN','Existing County',now());
            insert into "Caves"("Id","AccountId","StateId","CountyId","Name","AlternateNames","CountyNumber","IsArchived","CreatedOn") values('maincave01','mainacct01','mainstate1','maincnty01','Existing Main Cave','[]',42,false,now());
            """; await c.ExecuteNonQueryAsync();
        }
        await database.MigrateAsync(null);
        await using(var verify=database.CreateDbContext("main","mainacct01"))
        {
            var cave=await verify.Caves.IgnoreQueryFilters().SingleAsync(c=>c.Id=="maincave01"); Assert.Equal("Existing Main Cave",cave.Name); Assert.Equal("mainacct01",cave.AccountId); Assert.Null(cave.CurrentRevisionId);
        }
        await using var a=database.CreateDbContext("a","mainacct01"); await using var b=database.CreateDbContext("b","mainacct01");
        var ca=await a.Caves.IgnoreQueryFilters().SingleAsync(c=>c.Id=="maincave01"); var cb=await b.Caves.IgnoreQueryFilters().SingleAsync(c=>c.Id=="maincave01"); var version=ca.Version;
        ca.Name="Updated after upgrade"; await a.SaveChangesAsync(); Assert.NotEqual(version,ca.Version); cb.Name="Stale"; await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>b.SaveChangesAsync());
    }
}
