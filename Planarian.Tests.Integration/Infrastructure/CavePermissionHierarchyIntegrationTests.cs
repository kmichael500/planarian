using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;

namespace Planarian.Tests.Integration.Infrastructure;

public sealed class CavePermissionHierarchyIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Theory]
    [InlineData("cave")]
    [InlineData("county")]
    [InlineData("state")]
    [InlineData("account")]
    public async Task ManagerScopeAlsoGrantsView(string scope)
    {
        await using var database = await fixture.CreateDatabaseAsync($"manager_implies_view_{scope}");
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string userId = "manager-only";
        await GrantScopedAsync(database, cave, userId, scope, PermissionKey.Manager, "mAnageR000");

        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, userId);
        Assert.True(await actor.Db.RequestUser.HasCavePermission(PermissionKey.View, false));
        Assert.True(await actor.Db.RequestUser.HasCavePermission(
            PermissionKey.View, cave.CaveId, cave.CountyId, cave.StateId, false));
    }

    [Fact]
    public async Task ViewDoesNotGrantManager()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(ViewDoesNotGrantManager));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, cave, "view-only");
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "view-only");

        Assert.True(await actor.Db.RequestUser.HasCavePermission(
            PermissionKey.View, cave.CaveId, cave.CountyId, cave.StateId, false));
        Assert.False(await actor.Db.RequestUser.HasCavePermission(
            PermissionKey.Manager, cave.CaveId, cave.CountyId, cave.StateId, false));
    }

    private static async Task GrantScopedAsync(PostgresTestDatabase database, PublishedCaveTestData cave,
        string userId, string scope, string permissionKey, string permissionId)
    {
        await using var db = database.CreateDbContext(userId, cave.AccountId);
        await CavePermissions.EnsureAccountUserAsync(db, cave.AccountId);
        if (!await db.Permissions.AnyAsync(row => row.Id == permissionId))
        {
            db.Permissions.Add(new Permission
            {
                Id = permissionId, Key = permissionKey, Name = permissionKey,
                Description = permissionKey, PermissionType = "Cave"
            });
            await db.SaveChangesAsync();
        }

        db.CavePermissions.Add(new CavePermission
        {
            UserId = db.RequestUser.Id,
            AccountId = cave.AccountId,
            PermissionId = permissionId,
            CaveId = scope == "cave" ? cave.CaveId : null,
            CountyId = scope == "county" ? cave.CountyId : null,
            StateId = scope == "state" ? cave.StateId : null
        });
        await db.SaveChangesAsync();
    }
}
