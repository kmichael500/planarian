using Npgsql;
using Xunit;
using static Planarian.Tests.MigrationTestSupport;

namespace Planarian.Tests;

[Collection(MigrationIntegrationCollection.Name)]
public sealed class FileMetadataMigrationIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task CurrentMainV32RoundTripPreservesDistinctLegacyFileName()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(CurrentMainV32RoundTripPreservesDistinctLegacyFileName));
        var baseline = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));

        await using (var seed = new NpgsqlConnection(database.ConnectionString))
        {
            await seed.OpenAsync();
            await using var seedCommand = new NpgsqlCommand("""
                update "Files"
                set "FileName" = 'legacy-original.pdf', "DisplayName" = 'Friendly Label'
                where "Id" = 'mainfiler1'
                """, seed);
            await seedCommand.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using (var upgraded = new NpgsqlConnection(database.ConnectionString))
        {
            await upgraded.OpenAsync();
            await using var command = new NpgsqlCommand("""
                select f."Name", f."Extension", legacy."OriginalFileName",
                       legacy."MigratedName", legacy."MigratedExtension"
                from "Files" f
                join "__CaveRevisionsCompletionLegacyFileNames" legacy on legacy."FileId" = f."Id"
                where f."Id" = 'mainfiler1'
                """, upgraded);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Friendly Label", reader.GetString(0));
            Assert.Equal(".pdf", reader.GetString(1));
            Assert.Equal("legacy-original.pdf", reader.GetString(2));
            Assert.Equal("Friendly Label", reader.GetString(3));
            Assert.Equal(".pdf", reader.GetString(4));
        }

        await database.MigrateAsync(baseline);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var verifyCommand = new NpgsqlCommand("""
            select f."DisplayName", f."FileName",
                   (select column_default is null
                    from information_schema.columns
                    where table_name='Files' and column_name='FileName'),
                   to_regclass('public."__CaveRevisionsCompletionLegacyFileNames"') is null
            from "Files" f
            where f."Id" = 'mainfiler1'
            """, verify);
        await using var verifyReader = await verifyCommand.ExecuteReaderAsync();
        Assert.True(await verifyReader.ReadAsync());
        Assert.Equal("Friendly Label", verifyReader.GetString(0));
        Assert.Equal("legacy-original.pdf", verifyReader.GetString(1));
        Assert.True(verifyReader.GetBoolean(2));
        Assert.True(verifyReader.GetBoolean(3));
    }


    [Fact]
    public async Task CurrentMainV32RollbackUsesCurrentNameWhenFileMetadataChangedAfterUpgrade()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(CurrentMainV32RollbackUsesCurrentNameWhenFileMetadataChangedAfterUpgrade));
        var baseline = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));

        await using (var seed = new NpgsqlConnection(database.ConnectionString))
        {
            await seed.OpenAsync();
            await using var seedCommand = new NpgsqlCommand("""
                update "Files"
                set "FileName" = 'legacy-original.pdf', "DisplayName" = 'Friendly Label'
                where "Id" = 'mainfiler1'
                """, seed);
            await seedCommand.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using (var edit = new NpgsqlConnection(database.ConnectionString))
        {
            await edit.OpenAsync();
            await using var editCommand = new NpgsqlCommand("""
                update "Files"
                set "Name" = 'Renamed After Upgrade'
                where "Id" = 'mainfiler1'
                """, edit);
            await editCommand.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(baseline);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var verifyCommand = new NpgsqlCommand("""
            select "DisplayName", "FileName"
            from "Files"
            where "Id" = 'mainfiler1'
            """, verify);
        await using var reader = await verifyCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Renamed After Upgrade", reader.GetString(0));
        Assert.Equal("Renamed After Upgrade.pdf", reader.GetString(1));
    }


    [Fact]
    public async Task CurrentMainV32RollbackUsesCurrentExtensionWhenExtensionChangedAfterUpgrade()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(CurrentMainV32RollbackUsesCurrentExtensionWhenExtensionChangedAfterUpgrade));
        var baseline = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));

        await using (var seed = new NpgsqlConnection(database.ConnectionString))
        {
            await seed.OpenAsync();
            await using var seedCommand = new NpgsqlCommand("""
                update "Files"
                set "FileName" = 'legacy-original.pdf', "DisplayName" = 'Friendly Label'
                where "Id" = 'mainfiler1'
                """, seed);
            await seedCommand.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using (var edit = new NpgsqlConnection(database.ConnectionString))
        {
            await edit.OpenAsync();
            await using var editCommand = new NpgsqlCommand("""
                update "Files" set "Extension" = '.txt' where "Id" = 'mainfiler1'
                """, edit);
            await editCommand.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(baseline);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "DisplayName", "FileName" from "Files" where "Id" = 'mainfiler1'
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Friendly Label", reader.GetString(0));
        Assert.Equal("Friendly Label.txt", reader.GetString(1));
    }

    [Fact]
    public async Task CurrentMainV32RollbackReconstructsFileCreatedAfterUpgrade()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(CurrentMainV32RollbackReconstructsFileCreatedAfterUpgrade));
        var baseline = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(null);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var insertCommand = new NpgsqlCommand("""
                insert into "Files"("Id","AccountId","CaveId","FileTypeTagId","Name","Extension","BlobKey","BlobContainer","CreatedOn")
                values('newfile001','mainacct01','maincave01','mainfile01','Created Later','.PDF','new-file','main',now())
                """, connection);
            await insertCommand.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(baseline);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "DisplayName", "FileName" from "Files" where "Id"='newfile001'
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Created Later", reader.GetString(0));
        Assert.Equal("Created Later.PDF", reader.GetString(1));
    }

    [Fact]
    public async Task FileNameMigrationAcceptsExactMaximumCombinedLength()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(FileNameMigrationAcceptsExactMaximumCombinedLength));
        var displayName = new string('n', 996);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seedCommand = new NpgsqlCommand("""
                update "Files"
                set "DisplayName" = @display, "FileName" = 'legacy.pdf'
                where "Id" = 'mainfiler1'
                """, connection);
            seedCommand.Parameters.AddWithValue("display", displayName);
            await seedCommand.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "Name", "Extension" from "Files" where "Id"='mainfiler1'
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(displayName, reader.GetString(0));
        Assert.Equal(".pdf", reader.GetString(1));
    }

    [Fact]
    public async Task FileNameMigrationPreservesLegacyDisplayNameAndBackfillsNameAndExtensionEdgeCases()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(FileNameMigrationPreservesLegacyDisplayNameAndBackfillsNameAndExtensionEdgeCases));

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                update "Files" set "DisplayName" = 'Entrance Survey' where "Id" = 'mainfiler1';
                insert into "Files"("Id","AccountId","CaveId","FileTypeTagId","FileName","BlobKey","BlobContainer","CreatedOn") values
                  ('filemulti1','mainacct01','maincave01','mainfile01','survey.final.PDF','multi','main',now()),
                  ('fileplain1','mainacct01','maincave01','mainfile01','README','plain','main',now()),
                  ('filedot001','mainacct01','maincave01','mainfile01','.gitignore','dot','main',now()),
                  ('filetrail1','mainacct01','maincave01','mainfile01','survey.','trail','main',now()),
                  ('fileutf001','mainacct01','maincave01','mainfile01','Mügelhöhle.pdf','utf','main',now());
                update "Files" set "DisplayName" = '   ' where "Id" = 'fileplain1';
                """, connection);
            await seed.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        var results = new Dictionary<string, (string Name, string Extension)>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand(
                         "select \"Id\", \"Name\", \"Extension\" from \"Files\" order by \"Id\"", verify))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                results.Add(reader.GetString(0), (reader.GetString(1), reader.GetString(2)));
        }

        Assert.Equal(("Entrance Survey", ".pdf"), results["mainfiler1"]);
        Assert.Equal(("temporary", ".csv"), results["mainfilet1"]);
        Assert.Equal(("survey.final", ".PDF"), results["filemulti1"]);
        Assert.Equal(("README", ""), results["fileplain1"]);
        Assert.Equal((".gitignore", ""), results["filedot001"]);
        Assert.Equal(("survey.", ""), results["filetrail1"]);
        Assert.Equal(("Mügelhöhle", ".pdf"), results["fileutf001"]);

        await using var schema = new NpgsqlCommand("""
            select exists(select 1 from information_schema.columns where table_name='Files' and column_name='Name'),
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='Extension'),
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='FileName'),
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='DisplayName'),
                   exists(select 1 from pg_constraint where conname='CK_Files_ValidNameAndExtension')
            """, verify);
        await using var schemaReader = await schema.ExecuteReaderAsync();
        Assert.True(await schemaReader.ReadAsync());
        Assert.True(schemaReader.GetBoolean(0));
        Assert.True(schemaReader.GetBoolean(1));
        Assert.False(schemaReader.GetBoolean(2));
        Assert.False(schemaReader.GetBoolean(3));
        Assert.True(schemaReader.GetBoolean(4));
    }


    [Fact]
    public async Task FileNameMigrationFailsRatherThanTruncatingLegacyFileMetadata()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(FileNameMigrationFailsRatherThanTruncatingLegacyFileMetadata));
        var displayName = new string('n', 997);
        const string fileName = "legacy.pdf";

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                update "Files"
                set "DisplayName" = @display, "FileName" = @file
                where "Id" = 'mainfiler1'
                """, connection);
            seed.Parameters.AddWithValue("display", displayName);
            seed.Parameters.AddWithValue("file", fileName);
            await seed.ExecuteNonQueryAsync();
        }

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.MigrateAsync(null));
        Assert.Contains("would exceed 1000 characters", error.MessageText, StringComparison.Ordinal);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "DisplayName", "FileName",
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='Name'),
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='Extension')
            from "Files" where "Id"='mainfiler1'
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(displayName, reader.GetString(0));
        Assert.Equal(fileName, reader.GetString(1));
        Assert.False(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
    }


    [Theory]
    [InlineData("nested/report", "path-separator")]
    [InlineData("\u00A0", "unicode-whitespace")]
    public async Task FileNameMigrationFailsWhenLegacyDisplayNameViolatesTheNewNamePolicy(
        string legacyDisplayName, string databaseSuffix)
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, $"{nameof(FileNameMigrationFailsWhenLegacyDisplayNameViolatesTheNewNamePolicy)}_{databaseSuffix}");

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var update = new NpgsqlCommand("""
                update "Files"
                set "DisplayName" = @displayName
                where "Id" = 'mainfiler1'
                """, connection);
            update.Parameters.AddWithValue("displayName", legacyDisplayName);
            await update.ExecuteNonQueryAsync();
        }

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.MigrateAsync(null));
        Assert.Contains("legacy DisplayName contains a file name that is invalid", error.MessageText,
            StringComparison.Ordinal);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "DisplayName", "FileName",
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='Name'),
                   exists(select 1 from pg_constraint where conname='CK_Files_ValidNameAndExtension')
            from "Files" where "Id"='mainfiler1'
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(legacyDisplayName, reader.GetString(0));
        Assert.Equal("existing-cave.pdf", reader.GetString(1));
        Assert.False(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
    }


    [Theory]
    [InlineData("legacy.p/df", "path-separator")]
    [InlineData("legacy.p\ndf", "control-character")]
    public async Task FileNameMigrationFailsWhenLegacyExtensionViolatesTheNewPolicy(
        string legacyFileName, string databaseSuffix)
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, $"{nameof(FileNameMigrationFailsWhenLegacyExtensionViolatesTheNewPolicy)}_{databaseSuffix}");

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seedCommand = new NpgsqlCommand("""
                update "Files"
                set "FileName" = @fileName, "DisplayName" = 'Valid Name'
                where "Id" = 'mainfiler1'
                """, connection);
            seedCommand.Parameters.AddWithValue("fileName", legacyFileName);
            await seedCommand.ExecuteNonQueryAsync();
        }

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.MigrateAsync(null));
        Assert.Contains("legacy FileName contains an invalid file extension", error.MessageText,
            StringComparison.Ordinal);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "FileName",
                   not exists(select 1 from information_schema.columns where table_name='Files' and column_name='Name')
            from "Files" where "Id"='mainfiler1'
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(legacyFileName, reader.GetString(0));
        Assert.True(reader.GetBoolean(1));
    }

    [Fact]
    public async Task FileNameMigrationDownRestoresLegacyNameColumnsWithoutChangingTheFilename()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(FileNameMigrationDownRestoresLegacyNameColumnsWithoutChangingTheFilename));
        await database.MigrateAsync(null);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var update = new NpgsqlCommand("""
                update "Files"
                set "Name" = 'Entrance Survey', "Extension" = '.PDF'
                where "Id" = 'mainfiler1'
                """, connection);
            await update.ExecuteNonQueryAsync();
        }

        var previousMigration = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(previousMigration);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "DisplayName", "FileName",
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='Name'),
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='Extension'),
                   exists(select 1 from pg_constraint where conname='CK_Files_ValidNameAndExtension')
            from "Files" where "Id"='mainfiler1'
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Entrance Survey", reader.GetString(0));
        Assert.Equal("Entrance Survey.PDF", reader.GetString(1));
        Assert.False(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
        Assert.False(reader.GetBoolean(4));
    }


    [Fact]
    public async Task FileNameMigrationDownPreservesTheV32DisplayNameWidth()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(FileNameMigrationDownPreservesTheV32DisplayNameWidth));
        await database.MigrateAsync(null);
        var name = new string('n', 996);
        const string extension = ".pdf";

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var update = new NpgsqlCommand("""
                update "Files"
                set "Name" = @name, "Extension" = @extension
                where "Id" = 'mainfiler1'
                """, connection);
            update.Parameters.AddWithValue("name", name);
            update.Parameters.AddWithValue("extension", extension);
            await update.ExecuteNonQueryAsync();
        }

        var previousMigration = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(previousMigration);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "DisplayName", "FileName",
                   character_maximum_length
            from "Files"
            join information_schema.columns on table_name='Files' and column_name='DisplayName'
            where "Id"='mainfiler1'
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(name, reader.GetString(0));
        Assert.Equal(name + extension, reader.GetString(1));
        Assert.Equal(1000, reader.GetInt32(2));
    }

}
