using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static Planarian.Tests.MigrationTestSupport;

namespace Planarian.Tests;

[Collection(MigrationIntegrationCollection.Name)]
public sealed class LegacyReporterMigrationIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task LegacyReporterUserColumnsAreDroppedWhileExistingCaveEntranceAndPeopleTagDataSurvives()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(LegacyReporterUserColumnsAreDroppedWhileExistingCaveEntranceAndPeopleTagDataSurvives));

        const string reporterUserId = "reportusr1";
        const string caveReporterId = "caverep00a";
        const string entranceReporterId = "entrep000a";
        const string entranceId = "legacyent1";

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                insert into "Users"("Id","EmailAddress","FirstName","LastName","IsTemporary","SessionVersion","CreatedOn")
                values ('reportusr1','reporter@example.test','Legacy','Reporter',false,0,now());

                insert into "TagTypes"("Id","AccountId","Key","Name","IsDefault","CreatedOn") values
                  ('caverep00a','mainacct01','People','Cave Reporter',false,now()),
                  ('entrep000a','mainacct01','People','Entrance Reporter',false,now()),
                  ('locqual00a','mainacct01','LocationQuality','Survey Grade',false,now());

                insert into "Entrances"("Id","CaveId","LocationQualityTagId","Name","IsPrimary","Description","Location","CreatedOn")
                values ('legacyent1','maincave01','locqual00a','Legacy entrance',true,'Preserve me',
                        ST_SetSRID(ST_MakePoint(-86.25,35.15,612),4326),now());

                insert into "CaveReportedByNameTags"("Id","TagTypeId","CaveId","CreatedOn")
                values ('caverept01','caverep00a','maincave01',now());
                insert into "EntranceReportedByNameTags"("Id","TagTypeId","EntranceId","CreatedOn")
                values ('entrept001','entrep000a','legacyent1',now());

                update "Caves" set "ReportedByUserId" = 'reportusr1' where "Id" = 'maincave01';
                update "Entrances" set "ReportedByUserId" = 'reportusr1' where "Id" = 'legacyent1';
                """, connection);
            await seed.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using (var verify = database.CreateDbContext("verify", "mainacct01"))
        {
            Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == "maincave01"));
            var entrance = await verify.Entrances.IgnoreQueryFilters().SingleAsync(row => row.Id == entranceId);
            Assert.Equal("Preserve me", entrance.Description);
            Assert.Equal(-86.25, entrance.Location.X, 6);
            Assert.Equal(35.15, entrance.Location.Y, 6);
            Assert.Equal(612, entrance.Location.Z, 6);
            Assert.True(await verify.CaveReportedByNameTags.AnyAsync(tag =>
                tag.CaveId == "maincave01" && tag.TagTypeId == caveReporterId));
            Assert.True(await verify.EntranceReportedByNameTags.AnyAsync(tag =>
                tag.EntranceId == entranceId && tag.TagTypeId == entranceReporterId));
            Assert.True(await verify.Users.AnyAsync(user => user.Id == reporterUserId));
            Assert.True(await verify.TagTypes.AnyAsync(tag => tag.Id == caveReporterId));
            Assert.True(await verify.TagTypes.AnyAsync(tag => tag.Id == entranceReporterId));
        }

        await using var catalog = new NpgsqlConnection(database.ConnectionString);
        await catalog.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select
              not exists(select 1 from information_schema.columns where table_name='Caves' and column_name='ReportedByUserId'),
              not exists(select 1 from information_schema.columns where table_name='Entrances' and column_name='ReportedByUserId'),
              not exists(select 1 from pg_constraint where conname in ('FK_Caves_Users_ReportedByUserId', 'FK_Entrances_Users_ReportedByUserId')),
              not exists(select 1 from pg_indexes where indexname in ('IX_Caves_ReportedByUserId', 'IX_Entrances_ReportedByUserId'))
            """, catalog);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
    }

}
