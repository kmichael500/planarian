using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Tests.Integration.Infrastructure.Data;

/// <summary>
/// Seeds protected global/system prerequisites that intentionally exist outside tenant-owned runtime writes.
/// The production SaveChanges interceptor correctly protects these rows; tests that need
/// system bootstrap state use this narrow seed path instead of weakening authorization.
/// </summary>
internal static class GlobalSystemTestData
{
    public static async Task AddTagAsync(PostgresTestDatabase database, TagType tag)
    {
        await using var db = CreateSeedDbContext(database.ConnectionString);
        if (tag.CreatedOn == default) tag.CreatedOn = DateTime.UtcNow;
        db.TagTypes.Add(tag);
        await db.SaveChangesAsync();
    }

    public static async Task EnsurePermissionAsync(PostgresTestDatabase database, Permission permission)
    {
        await using var db = CreateSeedDbContext(database.ConnectionString);
        if (await db.Permissions.AnyAsync(row => row.Id == permission.Id)) return;
        if (permission.CreatedOn == default) permission.CreatedOn = DateTime.UtcNow;
        db.Permissions.Add(permission);
        await db.SaveChangesAsync();
    }

    public static async Task AddFeatureSettingAsync(PostgresTestDatabase database, FeatureSetting featureSetting)
    {
        await using var db = CreateSeedDbContext(database.ConnectionString);
        if (featureSetting.CreatedOn == default) featureSetting.CreatedOn = DateTime.UtcNow;
        db.FeatureSettings.Add(featureSetting);
        await db.SaveChangesAsync();
    }

    public static async Task AddUserPermissionAsync(
        PostgresTestDatabase database,
        string id,
        string userId,
        string permissionId)
    {
        await using var db = CreateSeedDbContext(database.ConnectionString);
        db.UserPermissions.Add(new UserPermission
        {
            Id = id,
            UserId = userId,
            PermissionId = permissionId,
            AccountId = null,
            CreatedOn = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static PlanarianDbContext CreateSeedDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql(connectionString, postgres =>
            {
                postgres.MigrationsAssembly("Planarian.Migrations");
                postgres.UseNetTopologySuite();
            })
            .Options;
        return new GlobalSystemSeedDbContext(options);
    }

    private sealed class GlobalSystemSeedDbContext(DbContextOptions<PlanarianDbContext> options)
        : PlanarianDbContext(options)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Intentionally do not call PlanarianDbContext.OnConfiguring: global reference data
            // is seeded outside request/account authorization, like production DataGenerator.
        }
    }
}
