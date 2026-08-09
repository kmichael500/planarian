using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Planarian.Model.Database;

namespace Planarian.Migrations.DesignTime;

public class DbContextDesignTimeFactory : IDesignTimeDbContextFactory<PlanarianDbContext>
{
    public PlanarianDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            // A clean checkout (including CI) intentionally has no local migration
            // settings file. Model inspection does not open a database connection,
            // so keep the file optional and use a harmless design-time fallback.
            .AddJsonFile("appsettings.Migrations.json", optional: true)
            .AddEnvironmentVariables();

        var configurator = builder.Build();

        var connectionString = configurator.GetConnectionString("default")
            ?? "Host=localhost;Database=planarian_design_time;Username=planarian;Password=planarian";

        var optionsBuilder = new DbContextOptionsBuilder<PlanarianDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o =>
        {
            o.MigrationsAssembly("Planarian.Migrations");
            o.UseNetTopologySuite();
            o.MaxBatchSize(1000);
        });


        var options = optionsBuilder.Options;

        return new PlanarianDbContext(options);
    }
}
