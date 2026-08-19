using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Xunit;

namespace Planarian.Tests.Integration.Infrastructure;

public sealed class RequestUserPermissionIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task PlanarianAdminDoesNotRequireSelectedAccount()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(PlanarianAdminDoesNotRequireSelectedAccount));
        const string userId = "plnadmin01";
        const string permissionId = "plnadmperm";

        await using (var seed = database.CreateDbContext(userId, null))
        {
            seed.Users.Add(new User("Planarian", "Admin", "planarian-admin@integration.test") { Id = userId });
            seed.Permissions.Add(new Permission
            {
                Id = permissionId,
                Key = PermissionKey.PlanarianAdmin,
                Name = "Planarian Admin",
                Description = "Global Planarian administration",
                PermissionType = PermissionType.User
            });
            seed.UserPermissions.Add(new UserPermission
            {
                UserId = userId,
                PermissionId = permissionId,
                AccountId = null
            });
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateDbContext(userId, null);
        await db.RequestUser.Initialize(null, userId);

        Assert.True(db.RequestUser.IsAuthenticated);
        Assert.Null(db.RequestUser.AccountId);
        Assert.True(await db.RequestUser.HasCavePermission(PermissionPolicyKey.PlanarianAdmin, false));
        Assert.False(await db.RequestUser.HasCavePermission(PermissionPolicyKey.Admin, false));
    }
}
