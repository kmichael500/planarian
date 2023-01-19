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
            .AddJsonFile("appsettings.Migrations.json")
            .AddEnvironmentVariables();

        var configurator = builder.Build();

        var connectionString = configurator.GetConnectionString("default");

        var optionsBuilder = new DbContextOptionsBuilder<PlanarianDbContext>();
        optionsBuilder.UseSqlServer(connectionString, o => { o.MigrationsAssembly("Planarian.Migrations"); });

        var options = optionsBuilder.Options;

        return new PlanarianDbContext(options);
    }
}