using Testcontainers.PostgreSql;
using Xunit;

namespace Planarian.Tests;

public sealed class PostgresSmokeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer? _container =
        Environment.GetEnvironmentVariable("PLANARIAN_RUN_POSTGRES_TESTS") == "1"
            ? new PostgreSqlBuilder().WithImage("postgis/postgis:16-3.4")
                .WithDatabase("planarian_tests").WithUsername("postgres").WithPassword("postgres").Build()
            : null;

    public Task InitializeAsync() => _container?.StartAsync() ?? Task.CompletedTask;
    public Task DisposeAsync() => _container?.DisposeAsync().AsTask() ?? Task.CompletedTask;

    [Fact]
    public async Task PostgisContainerAcceptsConnections()
    {
        // CI environments without Docker still build this test assembly; set
        // PLANARIAN_RUN_POSTGRES_TESTS=1 to execute the real PostGIS check.
        if (_container is null) return;
        await using var connection = new Npgsql.NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand("select postgis_full_version()", connection);
        Assert.False(string.IsNullOrWhiteSpace((string?)await command.ExecuteScalarAsync()));
    }
}
