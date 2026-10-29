using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Account.Model;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Concurrency;
using Planarian.Modules.Users.Repositories;
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

    [Theory]
    [InlineData("cave")]
    [InlineData("county")]
    [InlineData("state")]
    [InlineData("account")]
    public async Task ManagerPermissionAssignmentMatrixAllowsOnlySameOrNarrowerScopes(string grantedScope)
    {
        await using var database =
            await fixture.CreateDatabaseAsync($"permission_assignment_{CavePermissionMatrixTestSupport.ScopeCode(grantedScope)}");
        var world = await CavePermissionMatrixTestSupport.CreateWorldAsync(database);
        await EnsureViewPermissionAsync(database);

        var grantedScopeIndex = CavePermissionMatrixTestSupport.ScopeRank(grantedScope);
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

    [Theory]
    [InlineData("cave")]
    [InlineData("county")]
    [InlineData("state")]
    [InlineData("account")]
    public async Task ManagerPermissionRemovalMatrixAllowsOnlySameOrNarrowerScopes(string grantedScope)
    {
        await using var database =
            await fixture.CreateDatabaseAsync($"permission_removal_{CavePermissionMatrixTestSupport.ScopeCode(grantedScope)}");
        var world = await CavePermissionMatrixTestSupport.CreateWorldAsync(database);

        var grantedScopeIndex = CavePermissionMatrixTestSupport.ScopeRank(grantedScope);
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
    public async Task RevokeAccessAtomicallyRemovesMembershipAndAllAccountPermissions()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RevokeAccessAtomicallyRemovesMembershipAndAllAccountPermissions));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string adminId = "admin00001";
        const string targetUserId = "target0001";

        await EnsureAccountAdminPermissionAsync(database);
        await EnsureViewPermissionAsync(database);
        await EnsureAccountUserAsync(database, cave.AccountId, adminId);
        await EnsureAccountUserAsync(database, cave.AccountId, targetUserId);
        await GrantAccountAdminAsync(database, cave.AccountId, adminId);
        await CavePermissions.GrantViewAsync(database, cave, targetUserId);
        await GrantAccountAdminAsync(database, cave.AccountId, targetUserId);

        await using (var admin = database.CreateDbContext(adminId, cave.AccountId))
        {
            await CavePermissions.AuthenticateAsync(admin, cave.AccountId);
            await CreateService(admin).RevokeAccess(targetUserId, default);
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.False(await verify.AccountUsers.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == cave.AccountId && row.UserId == targetUserId));
        Assert.False(await verify.CavePermissions.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == cave.AccountId && row.UserId == targetUserId));
        Assert.False(await verify.UserPermissions.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == cave.AccountId && row.UserId == targetUserId));
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

    [Fact]
    public async Task PermissionManagementUsesDomainQualifiedSupportedRoles()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(PermissionManagementUsesDomainQualifiedSupportedRoles));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string adminId = "admin00001";
        const string targetUserId = "target0001";

        await EnsureAccountAdminPermissionAsync(database);
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "badcavadm1",
            Key = PermissionKey.Admin,
            Name = "Wrong Type Admin",
            Description = "Regression-only Cave permission with the Admin key.",
            PermissionType = PermissionType.Cave
        });
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "badusrmgr1",
            Key = PermissionKey.Manager,
            Name = "Wrong Type Manager",
            Description = "Regression-only User permission with the Manager key.",
            PermissionType = PermissionType.User
        });
        await EnsureAccountUserAsync(database, cave.AccountId, adminId);
        await EnsureAccountUserAsync(database, cave.AccountId, targetUserId);
        await GrantAccountAdminAsync(database, cave.AccountId, adminId);

        await using (var malformedSeed = database.CreateDbContext(targetUserId, cave.AccountId))
        {
            malformedSeed.UserPermissions.Add(new UserPermission
            {
                UserId = targetUserId,
                AccountId = cave.AccountId,
                PermissionId = "badusrmgr1"
            });
            await malformedSeed.SaveChangesAsync();
        }

        await using var admin = database.CreateDbContext(adminId, cave.AccountId);
        await CavePermissions.AuthenticateAsync(admin, cave.AccountId);
        var service = CreateService(admin);

        await service.AddUserPermission(targetUserId, PermissionKey.Admin);

        var caveAdminFailure = await Assert.ThrowsAsync<ApiException>(() =>
            service.UpdateCavePermissions(
                targetUserId,
                PermissionKey.Admin,
                new CreateUserCavePermissionsVm { CaveIds = [cave.CaveId] },
                default));
        Assert.Equal(400, caveAdminFailure.StatusCode);

        var userManagerFailure = await Assert.ThrowsAsync<ApiException>(() =>
            service.AddUserPermission(targetUserId, PermissionKey.Manager));
        Assert.Equal(400, userManagerFailure.StatusCode);

        var removeUserManagerFailure = await Assert.ThrowsAsync<ApiException>(() =>
            service.RemoveUserPermission(targetUserId, PermissionKey.Manager));
        Assert.Equal(400, removeUserManagerFailure.StatusCode);

        var caveReadFailure = await Assert.ThrowsAsync<ApiException>(() =>
            service.GetcavePermissions(targetUserId, PermissionKey.Admin));
        Assert.Equal(400, caveReadFailure.StatusCode);

        var invalidTypeFailure = await Assert.ThrowsAsync<ApiException>(() =>
            service.GetPermissionSelectList("Unsupported"));
        Assert.Equal(400, invalidTypeFailure.StatusCode);

        var userPermissionChoices = (await service.GetPermissionSelectList(PermissionType.User)).ToList();
        var onlyChoice = Assert.Single(userPermissionChoices);
        Assert.Equal(PermissionKey.Admin, onlyChoice.Data.Key);

        var managedPermissions = (await service.GetUserPermissions(targetUserId)).ToList();
        var managedAdmin = Assert.Single(managedPermissions);
        Assert.Equal(PermissionKey.Admin, managedAdmin.PermissionKey);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var assigned = await verify.UserPermissions
            .Include(row => row.Permission)
            .SingleAsync(row =>
                row.AccountId == cave.AccountId &&
                row.UserId == targetUserId &&
                row.Permission!.Key == PermissionKey.Admin);
        Assert.Equal(PermissionType.User, assigned.Permission!.PermissionType);
        Assert.Equal(PermissionKey.Admin, assigned.Permission.Key);
        Assert.True(await verify.UserPermissions.AnyAsync(row =>
            row.AccountId == cave.AccountId &&
            row.UserId == targetUserId &&
            row.Permission!.Key == PermissionKey.Manager &&
            row.Permission.PermissionType == PermissionType.User));
        Assert.False(await verify.CavePermissions.AnyAsync(row =>
            row.AccountId == cave.AccountId && row.UserId == targetUserId));
    }

    [Fact]
    public async Task CavePermissionGrantSerializesWithAccountRevocation()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(CavePermissionGrantSerializesWithAccountRevocation));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string adminId = "admin00001";
        const string targetUserId = "target0001";

        await EnsureAccountAdminPermissionAsync(database);
        await EnsureViewPermissionAsync(database);
        await EnsureAccountUserAsync(database, cave.AccountId, adminId);
        await EnsureAccountUserAsync(database, cave.AccountId, targetUserId);
        await GrantAccountAdminAsync(database, cave.AccountId, adminId);

        await using var grantDb = database.CreateDbContext(adminId, cave.AccountId);
        await CavePermissions.AuthenticateAsync(grantDb, cave.AccountId);
        await grantDb.Database.OpenConnectionAsync();
        await using var grantTransaction = await grantDb.Database.BeginTransactionAsync();

        await CreateService(grantDb).UpdateCavePermissions(
            targetUserId,
            PermissionKey.View,
            new CreateUserCavePermissionsVm { CaveIds = [cave.CaveId] },
            default,
            grantTransaction);

        await using var revokeDb = database.CreateDbContext(adminId, cave.AccountId);
        await CavePermissions.AuthenticateAsync(revokeDb, cave.AccountId);
        await revokeDb.Database.OpenConnectionAsync();
        var revokePid = await PostgresLockAssertions.BackendPidAsync(revokeDb);
        var revoke = CreateService(revokeDb).RevokeAccess(targetUserId, default);

        await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, revokePid, revoke);

        await grantTransaction.CommitAsync();
        await revoke;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.False(await verify.AccountUsers.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == cave.AccountId && row.UserId == targetUserId));
        Assert.False(await verify.CavePermissions.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == cave.AccountId && row.UserId == targetUserId));
    }

    [Fact]
    public async Task AccountAdminGrantSerializesWithAccountRevocation()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(AccountAdminGrantSerializesWithAccountRevocation));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string adminId = "admin00001";
        const string targetUserId = "target0001";

        await EnsureAccountAdminPermissionAsync(database);
        await EnsureAccountUserAsync(database, cave.AccountId, adminId);
        await EnsureAccountUserAsync(database, cave.AccountId, targetUserId);
        await GrantAccountAdminAsync(database, cave.AccountId, adminId);

        await using var grantDb = database.CreateDbContext(adminId, cave.AccountId);
        await CavePermissions.AuthenticateAsync(grantDb, cave.AccountId);
        await grantDb.Database.OpenConnectionAsync();
        await using var grantTransaction = await grantDb.Database.BeginTransactionAsync();

        await CreateService(grantDb).AddUserPermission(
            targetUserId, PermissionKey.Admin, default, grantTransaction);

        await using var revokeDb = database.CreateDbContext(adminId, cave.AccountId);
        await CavePermissions.AuthenticateAsync(revokeDb, cave.AccountId);
        await revokeDb.Database.OpenConnectionAsync();
        var revokePid = await PostgresLockAssertions.BackendPidAsync(revokeDb);
        var revoke = CreateService(revokeDb).RevokeAccess(targetUserId, default);

        await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, revokePid, revoke);

        await grantTransaction.CommitAsync();
        await revoke;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.False(await verify.AccountUsers.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == cave.AccountId && row.UserId == targetUserId));
        Assert.False(await verify.UserPermissions.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == cave.AccountId && row.UserId == targetUserId));
    }

    [Fact]
    public async Task CavePermissionGrantFailsClosedWhenRevocationWinsTheMembershipLock()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(CavePermissionGrantFailsClosedWhenRevocationWinsTheMembershipLock));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string adminId = "admin00001";
        const string targetUserId = "target0001";

        await EnsureAccountAdminPermissionAsync(database);
        await EnsureViewPermissionAsync(database);
        await EnsureAccountUserAsync(database, cave.AccountId, adminId);
        await EnsureAccountUserAsync(database, cave.AccountId, targetUserId);
        await GrantAccountAdminAsync(database, cave.AccountId, adminId);

        await using var revokeDb = database.CreateDbContext(adminId, cave.AccountId);
        await revokeDb.Database.OpenConnectionAsync();
        await using var revokeTransaction = await revokeDb.Database.BeginTransactionAsync();
        Assert.True(await new UserRepository(revokeDb, revokeDb.RequestUser)
            .DeleteAccountUserAccess(targetUserId, cave.AccountId));

        await using var grantDb = database.CreateDbContext(adminId, cave.AccountId);
        await CavePermissions.AuthenticateAsync(grantDb, cave.AccountId);
        await grantDb.Database.OpenConnectionAsync();
        var grantPid = await PostgresLockAssertions.BackendPidAsync(grantDb);
        var grant = CreateService(grantDb).UpdateCavePermissions(
            targetUserId,
            PermissionKey.View,
            new CreateUserCavePermissionsVm { CaveIds = [cave.CaveId] },
            default);

        await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, grantPid, grant);

        await revokeTransaction.CommitAsync();

        var failure = await Assert.ThrowsAsync<ApiException>(() => grant);
        Assert.Equal(404, failure.StatusCode);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.False(await verify.AccountUsers.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == cave.AccountId && row.UserId == targetUserId));
        Assert.False(await verify.CavePermissions.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == cave.AccountId && row.UserId == targetUserId));
    }


    [Fact]
    public async Task AccountAdminGrantFailsClosedWhenRevocationWinsTheMembershipLock()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(AccountAdminGrantFailsClosedWhenRevocationWinsTheMembershipLock));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string adminId = "admin00001";
        const string targetUserId = "target0001";

        await EnsureAccountAdminPermissionAsync(database);
        await EnsureAccountUserAsync(database, cave.AccountId, adminId);
        await EnsureAccountUserAsync(database, cave.AccountId, targetUserId);
        await GrantAccountAdminAsync(database, cave.AccountId, adminId);

        await using var revokeDb = database.CreateDbContext(adminId, cave.AccountId);
        await revokeDb.Database.OpenConnectionAsync();
        await using var revokeTransaction = await revokeDb.Database.BeginTransactionAsync();
        Assert.True(await new UserRepository(revokeDb, revokeDb.RequestUser)
            .DeleteAccountUserAccess(targetUserId, cave.AccountId));

        await using var grantDb = database.CreateDbContext(adminId, cave.AccountId);
        await CavePermissions.AuthenticateAsync(grantDb, cave.AccountId);
        await grantDb.Database.OpenConnectionAsync();
        var grantPid = await PostgresLockAssertions.BackendPidAsync(grantDb);
        var grant = CreateService(grantDb).AddUserPermission(targetUserId, PermissionKey.Admin);

        await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, grantPid, grant);

        await revokeTransaction.CommitAsync();

        var failure = await Assert.ThrowsAsync<ApiException>(() => grant);
        Assert.Equal(404, failure.StatusCode);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.False(await verify.AccountUsers.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == cave.AccountId && row.UserId == targetUserId));
        Assert.False(await verify.UserPermissions.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == cave.AccountId && row.UserId == targetUserId));
    }

}
