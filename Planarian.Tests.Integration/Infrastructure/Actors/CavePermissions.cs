using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Tests.Integration.Infrastructure.Actors;

internal static class CavePermissions
{
    public static Task GrantViewAsync(PostgresTestDatabase database, PublishedCaveTestData cave, string userId) =>
        GrantAsync(database, cave, userId, "vIeWPz9a00", "View", "View Caves");

    public static Task GrantManagerAsync(PostgresTestDatabase database, PublishedCaveTestData cave, string userId) =>
        GrantAsync(database, cave, userId, "mAnageR000", "Manager", "Manage Caves");

    public static async Task RevokeAllAsync(PostgresTestDatabase database, PublishedCaveTestData cave, string userId)
    {
        await using var db = database.CreateDbContext("permission-admin", cave.AccountId);
        await using var actor = database.CreateDbContext(userId, cave.AccountId);
        await db.CavePermissions.Where(row => row.AccountId == cave.AccountId && row.CaveId == cave.CaveId &&
            row.UserId == actor.RequestUser.Id).ExecuteDeleteAsync();
    }

    public static async Task RevokeManagerAsync(PostgresTestDatabase database, PublishedCaveTestData cave,
        string userId)
    {
        await using var db = database.CreateDbContext("permission-admin", cave.AccountId);
        await using var actor = database.CreateDbContext(userId, cave.AccountId);
        await db.CavePermissions.Where(row => row.AccountId == cave.AccountId && row.CaveId == cave.CaveId &&
            row.UserId == actor.RequestUser.Id && row.PermissionId == "mAnageR000").ExecuteDeleteAsync();
    }

    public static async Task EnsureAccountUserAsync(PlanarianDbContext db, string accountId)
    {
        if (await db.AccountUsers.AnyAsync(row => row.AccountId == accountId && row.UserId == db.RequestUser.Id))
            return;
        db.AccountUsers.Add(new AccountUser
        {
            AccountId = accountId,
            UserId = db.RequestUser.Id,
            InvitationAcceptedOn = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public static Task AuthenticateAsync(PlanarianDbContext db, string accountId) =>
        db.RequestUser.Initialize(accountId, db.RequestUser.Id);

    private static async Task GrantAsync(PostgresTestDatabase database, PublishedCaveTestData cave, string userId,
        string permissionId, string key, string description)
    {
        await using var db = database.CreateDbContext(userId, cave.AccountId);
        await EnsureAccountUserAsync(db, cave.AccountId);
        if (!await db.Permissions.AnyAsync(row => row.Id == permissionId))
        {
            db.Permissions.Add(new Permission
            {
                Id = permissionId, Key = key, Name = key, Description = description, PermissionType = "Cave"
            });
            await db.SaveChangesAsync();
        }
        db.CavePermissions.Add(new CavePermission
        {
            UserId = db.RequestUser.Id,
            AccountId = cave.AccountId,
            CaveId = cave.CaveId,
            PermissionId = permissionId
        });
        await db.SaveChangesAsync();
    }
}
