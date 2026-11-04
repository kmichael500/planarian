using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Planarian.Model.Database;
using Testcontainers.PostgreSql;

namespace Planarian.Tests.EmailDelivery.TestInfrastructure;

internal static class IsolatedPostgresDatabase
{
    public static async Task Run(Func<string, Task> test)
    {
        await using var postgres = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:16-3.4")
            .WithDatabase("account_access_tests")
            .WithUsername("planarian_test")
            .WithPassword("local_integration_test_only")
            .Build();
        await postgres.StartAsync();

        var connectionString = postgres.GetConnectionString();
        await using (var migrations = NewContext(connectionString))
            await migrations.Database.MigrateAsync();

        await test(connectionString);
    }

    public static PlanarianDbContext NewContext(string connectionString, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql(connectionString, database =>
            {
                database.MigrationsAssembly(typeof(Planarian.Migrations.Migrations.v33).Assembly.FullName);
                database.UseNetTopologySuite();
            });

        if (interceptors.Length > 0)
            options.AddInterceptors(interceptors);

        return new PlanarianDbContext(options.Options);
    }
}
