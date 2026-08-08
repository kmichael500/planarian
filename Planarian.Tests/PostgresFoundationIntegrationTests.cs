using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Planarian.Tests;

public sealed class PostgresFoundationIntegrationTests(PostgresIntegrationFixture fixture) : IClassFixture<PostgresIntegrationFixture>
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
            select conname
            from pg_constraint
            where contype = 'f'
              and conname like '%CaveRevisions%'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var foreignKeyNames = new List<string>();
        while (await reader.ReadAsync()) foreignKeyNames.Add(reader.GetString(0));

        Assert.Contains(foreignKeyNames, name => name.Contains("AccountId_CurrentRevisionId", StringComparison.Ordinal));
        Assert.Contains(foreignKeyNames, name => name.Contains("AccountId_ChangeRequestId", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CaveXminRejectsStaleWriterAndVersionChanges()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(CaveXminRejectsStaleWriterAndVersionChanges));
        var tenant = await IntegrationTestData.SeedTenantAsync(database, 'a');

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
        var tenant = await IntegrationTestData.SeedTenantAsync(database, 'a');

        await using var first = database.CreateDbContext("first", tenant.AccountId);
        await using var stale = database.CreateDbContext("stale", tenant.AccountId);
        var firstRequest = await first.CaveChangeRequests.SingleAsync(r => r.Id == tenant.ChangeRequestId);
        var staleRequest = await stale.CaveChangeRequests.SingleAsync(r => r.Id == tenant.ChangeRequestId);
        var originalVersion = firstRequest.Version;

        firstRequest.ReviewerNotes = "First writer";
        await first.SaveChangesAsync();
        Assert.NotEqual(originalVersion, firstRequest.Version);

        staleRequest.ReviewerNotes = "Stale writer";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
    }
}
