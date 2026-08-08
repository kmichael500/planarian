using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Testcontainers.PostgreSql;
using Xunit;

namespace Planarian.Tests;

/// <summary>Real PostgreSQL/PostGIS fixture. Docker is required; tests never opt out.</summary>
public sealed class PostgresIntegrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:16-3.4")
        .WithDatabase("planarian_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateDbContext("test-user", "test-account");
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public PlanarianDbContext CreateDbContext(string userId, string? accountId)
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql(ConnectionString, options =>
            {
                options.MigrationsAssembly("Planarian.Migrations");
                options.UseNetTopologySuite();
            })
            .Options;
        var db = new PlanarianDbContext(options);
        db.RequestUser = new RequestUser(db) { Id = userId, AccountId = accountId, FirstName = "Test", LastName = "User" };
        return db;
    }
}
