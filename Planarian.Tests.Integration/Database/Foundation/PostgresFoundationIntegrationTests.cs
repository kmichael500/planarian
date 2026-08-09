using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Planarian.Tests;

public sealed class PostgresFoundationIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task MigrationsCreatePostgisAndRevisionFoundation()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(MigrationsCreatePostgisAndRevisionFoundation));
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select postgis_version(),
                   exists(select 1 from information_schema.tables where table_name = 'CaveRevisions'),
                   exists(select 1 from information_schema.tables where table_name = 'CaveChangeRequests')
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.False(string.IsNullOrWhiteSpace(reader.GetString(0)));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
    }

    [Fact]
    public async Task DatabaseUsesRevisionTenantForeignKeys()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(DatabaseUsesRevisionTenantForeignKeys));
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select source.relname,
                   target.relname,
                   fk.confdeltype,
                   array_agg(source_column.attname order by source_key.ordinality),
                   array_agg(target_column.attname order by source_key.ordinality)
            from pg_constraint fk
            join pg_class source on source.oid = fk.conrelid
            join pg_class target on target.oid = fk.confrelid
            join lateral unnest(fk.conkey) with ordinality source_key(attnum, ordinality) on true
            join pg_attribute source_column on source_column.attrelid = source.oid and source_column.attnum = source_key.attnum
            join lateral unnest(fk.confkey) with ordinality target_key(attnum, ordinality)
                on target_key.ordinality = source_key.ordinality
            join pg_attribute target_column on target_column.attrelid = target.oid and target_column.attnum = target_key.attnum
            where fk.contype = 'f'
            group by source.relname, target.relname, fk.oid, fk.confdeltype
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var foreignKeys = new List<ForeignKeyShape>();
        while (await reader.ReadAsync())
            foreignKeys.Add(new(reader.GetString(0), reader.GetString(1), reader.GetChar(2).ToString(),
                reader.GetFieldValue<string[]>(3), reader.GetFieldValue<string[]>(4)));

        AssertForeignKey(foreignKeys, "Caves", ["AccountId", "Id", "CurrentRevisionId"], "CaveRevisions", ["AccountId", "CaveId", "Id"]);
        AssertForeignKey(foreignKeys, "CaveRevisions", ["AccountId", "CaveId", "PreviousRevisionId"], "CaveRevisions", ["AccountId", "CaveId", "Id"]);
        AssertForeignKey(foreignKeys, "CaveRevisions", ["AccountId", "CaveId", "ChangeRequestId"], "CaveChangeRequests", ["AccountId", "CaveId", "Id"]);
        AssertForeignKey(foreignKeys, "CaveRevisions", ["AccountId", "ImportBatchId"], "CaveImportBatches", ["AccountId", "Id"]);
        AssertForeignKey(foreignKeys, "CaveChangeRequests", ["AccountId", "CaveId", "BaseRevisionId"], "CaveRevisions", ["AccountId", "CaveId", "Id"]);
        AssertForeignKey(foreignKeys, "CaveChangeRequests", ["AccountId", "CaveId", "ApprovedRevisionId"], "CaveRevisions", ["AccountId", "CaveId", "Id"]);
        AssertForeignKey(foreignKeys, "CaveChangeRequests", ["AccountId", "Id", "CurrentProposalVersionId"], "CaveProposalVersions", ["AccountId", "ChangeRequestId", "Id"]);
        AssertForeignKey(foreignKeys, "CaveProposalVersions", ["AccountId", "ChangeRequestId", "PreviousProposalVersionId"], "CaveProposalVersions", ["AccountId", "ChangeRequestId", "Id"]);
        AssertForeignKey(foreignKeys, "CaveChangeRequestStagedFiles", ["AccountId", "ChangeRequestId"], "CaveChangeRequests", ["AccountId", "Id"]);
        AssertForeignKey(foreignKeys, "CaveChangeRequestStagedFiles", ["AccountId", "FileId"], "Files", ["AccountId", "Id"]);
    }

    [Fact]
    public async Task StagedFileForeignKeyRejectsForeignFileAndAllowsSameAccountFile()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(StagedFileForeignKeyRejectsForeignFileAndAllowsSameAccountFile));
        var a = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var b = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');
        var request = await TestDataBuilder.CreateChangeRequestAsync(database, a);
        var aFile = await TestDataBuilder.AddFileAsync(database, a);
        var bFile = await TestDataBuilder.AddFileAsync(database, b);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using (var sameAccount = new NpgsqlCommand("""
            insert into "Files" ("Id", "AccountId", "FileTypeTagId", "FileName", "CreatedOn")
            select 'samefile02', @account, "FileTypeTagId", 'same-account.pdf', now()
            from "Files" where "Id" = @existing_file;

            insert into "CaveChangeRequestStagedFiles" ("Id", "AccountId", "ChangeRequestId", "FileId", "CreatedOn")
            values ('samefile01', @account, @request, 'samefile02', now())
            """, connection))
        {
            sameAccount.Parameters.AddWithValue("account", a.AccountId);
            sameAccount.Parameters.AddWithValue("request", request.ChangeRequestId);
            sameAccount.Parameters.AddWithValue("existing_file", aFile.FileId);
            Assert.Equal(2, await sameAccount.ExecuteNonQueryAsync());
        }

        await using var foreignAccount = new NpgsqlCommand("""
            insert into "CaveChangeRequestStagedFiles" ("Id", "AccountId", "ChangeRequestId", "FileId", "CreatedOn")
            values ('foreignf01', @account, @request, @file, now())
            """, connection);
        foreignAccount.Parameters.AddWithValue("account", a.AccountId);
        foreignAccount.Parameters.AddWithValue("request", request.ChangeRequestId);
        foreignAccount.Parameters.AddWithValue("file", bFile.FileId);
        var error = await Assert.ThrowsAsync<PostgresException>(() => foreignAccount.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    private static void AssertForeignKey(IEnumerable<ForeignKeyShape> foreignKeys, string sourceTable,
        string[] sourceColumns, string targetTable, string[] targetColumns)
    {
        Assert.Contains(foreignKeys, key => key.SourceTable == sourceTable && key.TargetTable == targetTable &&
            key.DeleteAction == "r" && key.SourceColumns.SequenceEqual(sourceColumns) && key.TargetColumns.SequenceEqual(targetColumns));
    }

    private sealed record ForeignKeyShape(string SourceTable, string TargetTable, string DeleteAction,
        string[] SourceColumns, string[] TargetColumns);

    [Fact]
    public async Task CaveXminRejectsStaleWriterAndVersionChanges()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(CaveXminRejectsStaleWriterAndVersionChanges));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');

        await using var first = database.CreateDbContext("first", tenant.AccountId);
        await using var stale = database.CreateDbContext("stale", tenant.AccountId);
        var firstCave = await first.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == tenant.CaveId);
        var staleCave = await stale.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == tenant.CaveId);
        var originalVersion = firstCave.Version;

        firstCave.Name = "First writer";
        await first.SaveChangesAsync();
        Assert.NotEqual(originalVersion, firstCave.Version);

        staleCave.Name = "Stale writer";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
    }

    [Fact]
    public async Task CaveChangeRequestXminRejectsStaleWriterAndVersionChanges()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(CaveChangeRequestXminRejectsStaleWriterAndVersionChanges));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var request = await TestDataBuilder.CreateChangeRequestAsync(database, tenant);

        await using var first = database.CreateDbContext("first", tenant.AccountId);
        await using var stale = database.CreateDbContext("stale", tenant.AccountId);
        var firstRequest = await first.CaveChangeRequests.SingleAsync(r => r.Id == request.ChangeRequestId);
        var staleRequest = await stale.CaveChangeRequests.SingleAsync(r => r.Id == request.ChangeRequestId);
        var originalVersion = firstRequest.Version;

        firstRequest.ReviewerNotes = "First writer";
        await first.SaveChangesAsync();
        Assert.NotEqual(originalVersion, firstRequest.Version);

        staleRequest.ReviewerNotes = "Stale writer";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
    }
}
