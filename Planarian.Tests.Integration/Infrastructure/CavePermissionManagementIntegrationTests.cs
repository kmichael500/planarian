using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Account.Model;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;
using static Planarian.Tests.Integration.Infrastructure.CavePermissionManagementTestSupport;

namespace Planarian.Tests.Integration.Infrastructure;

public sealed class CavePermissionManagementIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task CaveManagerCanGrantOwnedCaveButCannotPromoteThatAuthorityToTheState()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(CaveManagerCanGrantOwnedCaveButCannotPromoteThatAuthorityToTheState));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string managerId = "manager001";
        const string targetUserId = "target0001";

        await CavePermissions.GrantManagerAsync(database, cave, managerId);
        await EnsureViewPermissionAsync(database);
        await EnsureAccountUserAsync(database, cave.AccountId, targetUserId);

        await using var manager = database.CreateDbContext(managerId, cave.AccountId);
        await CavePermissions.AuthenticateAsync(manager, cave.AccountId);
        var service = CreateService(manager);

        await service.UpdateCavePermissions(
            targetUserId,
            PermissionKey.View,
            new CreateUserCavePermissionsVm { CaveIds = [cave.CaveId] },
            default);

        var failure = await Assert.ThrowsAsync<ApiException>(() =>
            service.UpdateCavePermissions(
                targetUserId,
                PermissionKey.View,
                new CreateUserCavePermissionsVm
                {
                    StateIds = [cave.StateId],
                    CaveIds = [cave.CaveId]
                },
                default));
        Assert.Equal(403, failure.StatusCode);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var permissions = await verify.CavePermissions
            .Where(row => row.AccountId == cave.AccountId && row.UserId == targetUserId)
            .ToListAsync();

        var ownedCave = Assert.Single(permissions);
        Assert.Equal(cave.CaveId, ownedCave.CaveId);
        Assert.Null(ownedCave.CountyId);
        Assert.Null(ownedCave.StateId);
    }

    [Fact]
    public async Task ManagerPermissionAssignmentMatrixAllowsOnlySameOrNarrowerScopes()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(ManagerPermissionAssignmentMatrixAllowsOnlySameOrNarrowerScopes));
        var world = await CavePermissionMatrixTestSupport.CreateWorldAsync(database);
        await EnsureViewPermissionAsync(database);

        for (var grantedScopeIndex = 0; grantedScopeIndex < CavePermissionMatrixTestSupport.Scopes.Length; grantedScopeIndex++)
        {
            var grantedScope = CavePermissionMatrixTestSupport.Scopes[grantedScopeIndex];
            var managerId = $"mgr{grantedScopeIndex}000001";
            await CavePermissionMatrixTestSupport.GrantScopedAsync(
                database, world, managerId, grantedScope, PermissionKey.Manager);

            await using var manager = database.CreateDbContext(managerId, world.AccountId);
            await CavePermissions.AuthenticateAsync(manager, world.AccountId);
            var service = CreateService(manager);

            for (var requestedScopeIndex = 0;
                 requestedScopeIndex < CavePermissionMatrixTestSupport.Scopes.Length;
                 requestedScopeIndex++)
            {
                var requestedScope = CavePermissionMatrixTestSupport.Scopes[requestedScopeIndex];
                foreach (var (permissionKey, permissionIndex) in new[]
                         {
                             (PermissionKey.View, 0),
                             (PermissionKey.Manager, 1)
                         })
                {
                    var targetUserId = $"t{grantedScopeIndex}{requestedScopeIndex}{permissionIndex}00000";
                    await EnsureAccountUserAsync(database, world.AccountId, targetUserId);
                    var desired = PermissionVmForScope(requestedScope, world.Target);
                    var expected = CavePermissionMatrixTestSupport.ScopeRank(grantedScope) >=
                                   CavePermissionMatrixTestSupport.ScopeRank(requestedScope);

                    if (expected)
                    {
                        await service.UpdateCavePermissions(targetUserId, permissionKey, desired, default);
                    }
                    else
                    {
                        var failure = await Assert.ThrowsAsync<ApiException>(() =>
                            service.UpdateCavePermissions(targetUserId, permissionKey, desired, default));
                        Assert.Equal(403, failure.StatusCode);
                    }

                    await using var verify = database.CreateDbContext("verify", world.AccountId);
                    var persisted = await verify.CavePermissions
                        .Where(row =>
                            row.AccountId == world.AccountId &&
                            row.UserId == targetUserId &&
                            row.Permission!.Key == permissionKey)
                        .ToListAsync();

                    if (!expected)
                    {
                        Assert.Empty(persisted);
                        continue;
                    }

                    var granted = Assert.Single(persisted);
                    Assert.Equal(requestedScope == "cave" ? world.Target.CaveId : null, granted.CaveId);
                    Assert.Equal(requestedScope == "county" ? world.Target.CountyId : null, granted.CountyId);
                    Assert.Equal(requestedScope == "state" ? world.Target.StateId : null, granted.StateId);
                }
            }
        }
    }

    [Fact]
    public async Task ManagerPermissionRemovalMatrixAllowsOnlySameOrNarrowerScopes()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(ManagerPermissionRemovalMatrixAllowsOnlySameOrNarrowerScopes));
        var world = await CavePermissionMatrixTestSupport.CreateWorldAsync(database);

        for (var grantedScopeIndex = 0; grantedScopeIndex < CavePermissionMatrixTestSupport.Scopes.Length; grantedScopeIndex++)
        {
            var grantedScope = CavePermissionMatrixTestSupport.Scopes[grantedScopeIndex];
            var managerId = $"rm{grantedScopeIndex}0000001";
            await CavePermissionMatrixTestSupport.GrantScopedAsync(
                database, world, managerId, grantedScope, PermissionKey.Manager);

            await using var manager = database.CreateDbContext(managerId, world.AccountId);
            await CavePermissions.AuthenticateAsync(manager, world.AccountId);
            var service = CreateService(manager);

            for (var existingScopeIndex = 0;
                 existingScopeIndex < CavePermissionMatrixTestSupport.Scopes.Length;
                 existingScopeIndex++)
            {
                var existingScope = CavePermissionMatrixTestSupport.Scopes[existingScopeIndex];
                foreach (var (permissionKey, permissionIndex) in new[]
                         {
                             (PermissionKey.View, 0),
                             (PermissionKey.Manager, 1)
                         })
                {
                    var targetUserId = $"r{grantedScopeIndex}{existingScopeIndex}{permissionIndex}00000";
                    await CavePermissionMatrixTestSupport.GrantScopedAsync(
                        database, world, targetUserId, existingScope, permissionKey);

                    var expected = CavePermissionMatrixTestSupport.ScopeRank(grantedScope) >=
                                   CavePermissionMatrixTestSupport.ScopeRank(existingScope);

                    if (expected)
                    {
                        await service.UpdateCavePermissions(
                            targetUserId, permissionKey, new CreateUserCavePermissionsVm(), default);
                    }
                    else
                    {
                        var failure = await Assert.ThrowsAsync<ApiException>(() =>
                            service.UpdateCavePermissions(
                                targetUserId, permissionKey, new CreateUserCavePermissionsVm(), default));
                        Assert.Equal(403, failure.StatusCode);
                    }

                    await using var verify = database.CreateDbContext("verify", world.AccountId);
                    var persisted = await verify.CavePermissions
                        .Where(row =>
                            row.AccountId == world.AccountId &&
                            row.UserId == targetUserId &&
                            row.Permission!.Key == permissionKey)
                        .ToListAsync();

                    if (expected)
                    {
                        Assert.Empty(persisted);
                    }
                    else
                    {
                        var preserved = Assert.Single(persisted);
                        Assert.Equal(existingScope == "cave" ? world.Target.CaveId : null, preserved.CaveId);
                        Assert.Equal(existingScope == "county" ? world.Target.CountyId : null, preserved.CountyId);
                        Assert.Equal(existingScope == "state" ? world.Target.StateId : null, preserved.StateId);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task ScopedManagerCanPreserveBroaderGrantWhileEditingOnlyOwnedScope()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(ScopedManagerCanPreserveBroaderGrantWhileEditingOnlyOwnedScope));
        var world = await CavePermissionMatrixTestSupport.CreateWorldAsync(database);
        var actorCave = world.Caves.Single(cave => cave.StateId != world.Target.StateId);
        const string managerId = "presmgr001";
        const string targetUserId = "prestgt001";

        await CavePermissionMatrixTestSupport.GrantScopedAsync(
            database, world.AccountId, actorCave, managerId, "cave", PermissionKey.Manager);
        await CavePermissionMatrixTestSupport.GrantScopedAsync(
            database, world, targetUserId, "state", PermissionKey.View);

        await using var manager = database.CreateDbContext(managerId, world.AccountId);
        await CavePermissions.AuthenticateAsync(manager, world.AccountId);
        var service = CreateService(manager);

        var existing = await service.GetcavePermissions(targetUserId, PermissionKey.View);
        Assert.Contains(world.Target.StateId, existing.StateCountyValues.States);

        await service.UpdateCavePermissions(
            targetUserId,
            PermissionKey.View,
            new CreateUserCavePermissionsVm
            {
                StateIds = [world.Target.StateId],
                CaveIds = [actorCave.CaveId]
            },
            default);

        await using var verify = database.CreateDbContext("verify", world.AccountId);
        var persisted = await verify.CavePermissions
            .Where(row =>
                row.AccountId == world.AccountId &&
                row.UserId == targetUserId &&
                row.Permission!.Key == PermissionKey.View)
            .ToListAsync();

        Assert.Contains(persisted, row =>
            row.StateId == world.Target.StateId &&
            row.CountyId == null &&
            row.CaveId == null);
        Assert.Contains(persisted, row =>
            row.CaveId == actorCave.CaveId &&
            row.CountyId == null &&
            row.StateId == null);
        Assert.Equal(2, persisted.Count);
    }

    [Fact]
    public async Task ScopedManagerCannotAssignAccountAdminPermission()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(ScopedManagerCannotAssignAccountAdminPermission));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string managerId = "manager001";
        const string targetUserId = "target0001";

        await CavePermissions.GrantManagerAsync(database, cave, managerId);
        await EnsureAccountAdminPermissionAsync(database);
        await EnsureAccountUserAsync(database, cave.AccountId, targetUserId);

        await using var manager = database.CreateDbContext(managerId, cave.AccountId);
        await CavePermissions.AuthenticateAsync(manager, cave.AccountId);

        var failure = await Assert.ThrowsAsync<ApiException>(() =>
            CreateService(manager).AddUserPermission(targetUserId, PermissionKey.Admin));

        Assert.Equal(403, failure.StatusCode);
        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.False(await verify.UserPermissions
            .AnyAsync(row => row.AccountId == cave.AccountId && row.UserId == targetUserId));
    }

    [Fact]
    public async Task AccountAdminCanAssignAccountAdminButCannotAssignPlanarianAdmin()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(AccountAdminCanAssignAccountAdminButCannotAssignPlanarianAdmin));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string adminId = "admin00001";
        const string targetUserId = "target0001";

        await EnsureAccountAdminPermissionAsync(database);
        await EnsurePlanarianAdminPermissionAsync(database);
        await EnsureAccountUserAsync(database, cave.AccountId, adminId);
        await EnsureAccountUserAsync(database, cave.AccountId, targetUserId);
        await GrantAccountAdminAsync(database, cave.AccountId, adminId);

        await using var admin = database.CreateDbContext(adminId, cave.AccountId);
        await CavePermissions.AuthenticateAsync(admin, cave.AccountId);
        var service = CreateService(admin);

        await service.AddUserPermission(targetUserId, PermissionKey.Admin);

        var platformFailure = await Assert.ThrowsAsync<ApiException>(() =>
            service.AddUserPermission(targetUserId, PermissionKey.PlanarianAdmin));
        Assert.Equal(400, platformFailure.StatusCode);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.True(await verify.UserPermissions.AnyAsync(row =>
            row.AccountId == cave.AccountId &&
            row.UserId == targetUserId &&
            row.Permission!.Key == PermissionKey.Admin));
        Assert.False(await verify.UserPermissions.AnyAsync(row =>
            row.UserId == targetUserId &&
            row.Permission!.Key == PermissionKey.PlanarianAdmin));
    }

}
