using Npgsql;
using Xunit;

namespace Planarian.Tests;

public sealed class PostgresFoundationIntegrationTests(PostgresIntegrationFixture fixture) : IClassFixture<PostgresIntegrationFixture>
{
    [Fact]
    public async Task MigrationsCreatePostgisAndRevisionFoundation()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select postgis_version(),
                   exists(select 1 from information_schema.tables where table_name = 'CaveRevisions'),
                   exists(select 1 from information_schema.columns where table_name = 'CaveChangeRequests' and column_name = 'xmin')
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
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
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
}
