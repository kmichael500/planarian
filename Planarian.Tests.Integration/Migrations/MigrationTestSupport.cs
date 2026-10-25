using Npgsql;

namespace Planarian.Tests;

internal static class MigrationTestSupport
{
    internal const string HistoricalV29BaselineMigration = "20260423021136_v29";
    internal const string CurrentMainBaselineMigration = "20260831132210_v32";
    internal const string CaveRevisionsCompletionMigrationSuffix = "_CaveRevisionsCompletion";

    internal static async Task<PostgresTestDatabase> CreateCurrentMainV32DatabaseAsync(
        PostgresTestServer fixture, string name, bool seedRepresentativeData = true)
    {
        var database = await fixture.CreateUnmigratedDatabaseAsync(name);
        var migration = database.GetMigrationNames().Single(candidate =>
            candidate.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(migration);
        if (seedRepresentativeData)
            await SeedCurrentMainV32RepresentativeDataAsync(database);
        return database;
    }

    internal static async Task SeedCurrentMainV32RepresentativeDataAsync(PostgresTestDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into "States"("Id","Name","Abbreviation","CreatedOn")
            values('mainstate1','Tennessee','TN',now());

            insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn")
            values('mainacct01','Existing Main Account','-',false,true,now());

            insert into "Counties"("Id","AccountId","StateId","DisplayId","Name","CreatedOn")
            values('maincnty01','mainacct01','mainstate1','MAIN','Existing County',now());

            insert into "Caves"("Id","AccountId","StateId","CountyId","Name","AlternateNames","CountyNumber","IsArchived","CreatedOn")
            values('maincave01','mainacct01','mainstate1','maincnty01','Existing Main Cave','[]',42,false,now());

            insert into "TagTypes"("Id","AccountId","Key","Name","IsDefault","CreatedOn")
            values('mainfile01','mainacct01','File','Existing file',false,now());

            insert into "Files"("Id","AccountId","CaveId","FileTypeTagId","FileName","DisplayName","BlobKey","BlobContainer","CreatedOn") values
              ('mainfiler1','mainacct01','maincave01','mainfile01','existing-cave.pdf','existing-cave',
               'caves/maincave01/files/mainfiler1.pdf','main',now()),
              ('mainfilet1','mainacct01',null,'mainfile01','temporary.csv','temporary',
               'temp/import/caves/mainfilet1.csv','main',now());
            """;
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task<IReadOnlyList<string>> CapturePublicSchemaContractAsync(PostgresTestDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select contract
            from (
                select 'column|' || table_name || '|' || column_name || '|' ||
                       data_type || '|' || udt_name || '|' ||
                       coalesce(character_maximum_length::text, '') || '|' ||
                       is_nullable || '|' || coalesce(column_default, '') as contract
                from information_schema.columns
                where table_schema = 'public'

                union all

                select 'constraint|' || relation.relname || '|' || constraint_row.conname || '|' ||
                       constraint_row.contype::text || '|' || pg_get_constraintdef(constraint_row.oid, true)
                from pg_constraint constraint_row
                join pg_class relation on relation.oid = constraint_row.conrelid
                join pg_namespace schema_row on schema_row.oid = relation.relnamespace
                where schema_row.nspname = 'public'

                union all

                select 'index|' || tablename || '|' || indexname || '|' || indexdef
                from pg_indexes
                where schemaname = 'public'
            ) contract_rows
            order by contract
            """, connection);
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));
        return result;
    }
}
