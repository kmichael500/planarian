using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Users.Repositories;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;
using static Planarian.Tests.Integration.Infrastructure.CavePermissionMatrixTestSupport;

namespace Planarian.Tests.Integration.Infrastructure;

public sealed class CavePermissionMatrixIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ScopedCavePermissionMatrixMatchesHierarchyAndExactScopeExpansion()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(ScopedCavePermissionMatrixMatchesHierarchyAndExactScopeExpansion));
        var world = await CreateWorldAsync(database);

        foreach (var scope in Scopes)
        {
            foreach (var grantedPermission in GrantedCavePermissions)
            {
                var userId = MatrixUserId(scope, grantedPermission);
                await GrantScopedAsync(database, world, userId, scope, grantedPermission);

                await using var actor = await CaveTestActor.CreateAsync(database, world.AccountId, userId);
                var expectedCaveIds = ExpectedCaves(world, scope);

                var projectedCaveIds = await actor.Db.UserCavePermissionView
                    .Where(row =>
                        row.AccountId == world.AccountId &&
                        row.UserId == actor.Db.RequestUser.Id &&
                        row.PermissionKey == grantedPermission &&
                        row.CaveId != null)
                    .Select(row => row.CaveId!)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToListAsync();

                Assert.Equal(expectedCaveIds.OrderBy(id => id), projectedCaveIds);

                foreach (var cave in world.Caves)
                {
                    foreach (var requestedPermission in RequestedPermissions)
                    {
                        var expected = expectedCaveIds.Contains(cave.CaveId, StringComparer.Ordinal) &&
                                       Satisfies(grantedPermission, requestedPermission);
                        var actual = await actor.Db.RequestUser.HasCavePermission(
                            requestedPermission, cave.CaveId, cave.CountyId, cave.StateId, false);

                        Assert.True(actual == expected,
                            $"Grant={grantedPermission}, scope={scope}, request={requestedPermission}, " +
                            $"cave={cave.CaveId}: expected {expected}, got {actual}.");
                    }
                }

                foreach (var requestedPermission in RequestedPermissions)
                {
                    var expected = Satisfies(grantedPermission, requestedPermission);
                    var actual = await actor.Db.RequestUser.HasCavePermission(requestedPermission, false);
                    Assert.True(actual == expected,
                        $"Grant={grantedPermission}, scope={scope}, account-wide request={requestedPermission}: " +
                        $"expected {expected}, got {actual}.");
                }
            }
        }
    }

    [Fact]
    public async Task ScopeAuthorityMatrixMatchesRoleAndScopeLattices()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(ScopeAuthorityMatrixMatchesRoleAndScopeLattices));
        var world = await CreateWorldAsync(database);

        foreach (var grantedScope in Scopes)
        {
            foreach (var grantedPermission in GrantedCavePermissions)
            {
                var userId = $"s{ScopeCode(grantedScope)}{grantedPermission[0]}00000";
                await GrantScopedAsync(database, world, userId, grantedScope, grantedPermission);
                await using var actor = await CaveTestActor.CreateAsync(database, world.AccountId, userId);

                foreach (var requestedScope in Scopes)
                {
                    foreach (var requestedPermission in RequestedPermissions)
                    {
                        var expected = ScopeRank(grantedScope) >= ScopeRank(requestedScope) &&
                                       Satisfies(grantedPermission, requestedPermission);
                        var actual = await HasScopePermissionAsync(
                            actor, world.Target, requestedScope, requestedPermission);

                        Assert.True(actual == expected,
                            $"Grant={grantedPermission}@{grantedScope}, " +
                            $"request={requestedPermission}@{requestedScope}: expected {expected}, got {actual}.");
                    }
                }
            }
        }
    }

    [Fact]
    public async Task AllLocationsManagerCapabilitySurvivesAnEmptyAccount()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(AllLocationsManagerCapabilitySurvivesAnEmptyAccount));
        const string accountId = "emptyacct1";
        const string userId = "emptymgr01";
        const string permissionId = "emptymgr01";

        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = permissionId,
            Key = PermissionKey.Manager,
            Name = PermissionKey.Manager,
            Description = "Manage Caves",
            PermissionType = PermissionType.Cave
        });

        await using (var seed = database.CreateDbContext(userId, accountId))
        {
            seed.Accounts.Add(new Account
            {
                Id = accountId,
                Name = "Empty Account",
                CountyIdDelimiter = "-",
                ExportEnabled = false
            });
            await seed.SaveChangesAsync();
            await CavePermissions.EnsureAccountUserAsync(seed, accountId);
            seed.CavePermissions.Add(new CavePermission
            {
                UserId = seed.RequestUser.Id,
                AccountId = accountId,
                PermissionId = permissionId
            });
            await seed.SaveChangesAsync();
        }

        await using var actor = await CaveTestActor.CreateAsync(database, accountId, userId);

        Assert.True(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.Manager, false));
        Assert.True(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.View, false));
        Assert.True(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.AdminManager, false));
        Assert.False(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.Admin, false));
        Assert.Empty(await actor.Db.UserCavePermissionView
            .Where(row => row.UserId == actor.Db.RequestUser.Id)
            .ToListAsync());

        var clientPermissions = (await new UserRepository(actor.Db, actor.Db.RequestUser)
                .GetPermissions(actor.Db.RequestUser.Id, accountId))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(PermissionPolicyKey.Manager, clientPermissions);
        Assert.Contains(PermissionPolicyKey.AdminManager, clientPermissions);
    }

    [Fact]
    public async Task NoCavePermissionFailsClosedAcrossEveryScopeSensitiveRequest()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(NoCavePermissionFailsClosedAcrossEveryScopeSensitiveRequest));
        var world = await CreateWorldAsync(database);
        const string userId = "noperm0001";

        await using var actor = await CaveTestActor.CreateAsync(database, world.AccountId, userId);

        foreach (var cave in world.Caves)
        {
            foreach (var requestedPermission in RequestedPermissions)
            {
                Assert.False(await actor.Db.RequestUser.HasCavePermission(
                    requestedPermission, cave.CaveId, cave.CountyId, cave.StateId, false));
            }
        }

        foreach (var requestedPermission in RequestedPermissions)
            Assert.False(await actor.Db.RequestUser.HasCavePermission(requestedPermission, false));

        Assert.Empty(await actor.Db.UserCavePermissionView
            .Where(row => row.UserId == actor.Db.RequestUser.Id)
            .ToListAsync());
    }

    [Fact]
    public async Task AccountWideManagerGetsOnlyManagerDerivedAccountCapabilities()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(AccountWideManagerGetsOnlyManagerDerivedAccountCapabilities));
        var world = await CreateWorldAsync(database);
        const string userId = "allmgr0001";

        await SetExportEnabledAsync(database, world.AccountId, false);
        await GrantScopedAsync(database, world, userId, "account", PermissionKey.Manager);

        await using var actor = await CaveTestActor.CreateAsync(database, world.AccountId, userId);

        Assert.True(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.View, false));
        Assert.True(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.Manager, false));
        Assert.True(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.AdminManager, false));
        Assert.False(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.Admin, false));
        Assert.False(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.Export, false));
        Assert.False(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.PlanarianAdmin, false));

        var clientPermissions = (await new UserRepository(actor.Db, actor.Db.RequestUser)
                .GetPermissions(actor.Db.RequestUser.Id, world.AccountId))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(PermissionPolicyKey.Manager, clientPermissions);
        Assert.Contains(PermissionPolicyKey.AdminManager, clientPermissions);
        Assert.DoesNotContain(PermissionPolicyKey.Admin, clientPermissions);
        Assert.DoesNotContain(PermissionPolicyKey.Export, clientPermissions);
        Assert.DoesNotContain(PermissionPolicyKey.PlanarianAdmin, clientPermissions);
    }

    [Theory]
    [InlineData("cave")]
    [InlineData("county")]
    [InlineData("state")]
    public async Task ScopedManagerDoesNotProjectSyntheticAdminManager(string scope)
    {
        await using var database =
            await fixture.CreateDatabaseAsync($"scoped_manager_projection_{scope}");
        var world = await CreateWorldAsync(database);
        var userId = $"p{ScopeCode(scope)}m00000";

        await GrantScopedAsync(database, world, userId, scope, PermissionKey.Manager);
        await using var actor = await CaveTestActor.CreateAsync(database, world.AccountId, userId);

        var clientPermissions = (await new UserRepository(actor.Db, actor.Db.RequestUser)
                .GetPermissions(actor.Db.RequestUser.Id, world.AccountId))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(PermissionPolicyKey.Manager, clientPermissions);
        Assert.DoesNotContain(PermissionPolicyKey.AdminManager, clientPermissions);
        Assert.False(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.AdminManager, false));
    }

    [Fact]
    public async Task AccountAdminDominatesAccountAndCaveHierarchyButNotPlatformAdministration()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(
                nameof(AccountAdminDominatesAccountAndCaveHierarchyButNotPlatformAdministration));
        var world = await CreateWorldAsync(database);
        const string userId = "acctadm001";

        await SetExportEnabledAsync(database, world.AccountId, false);
        await GrantAccountAdminAsync(database, world.AccountId, userId);

        await using var actor = await CaveTestActor.CreateAsync(database, world.AccountId, userId);

        foreach (var requestedPermission in RequestedPermissions)
        {
            Assert.True(await actor.Db.RequestUser.HasCavePermission(requestedPermission, false));
            foreach (var cave in world.Caves)
            {
                Assert.True(await actor.Db.RequestUser.HasCavePermission(
                    requestedPermission, cave.CaveId, cave.CountyId, cave.StateId, false));
            }
        }

        Assert.True(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.AdminManager, false));
        Assert.True(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.Export, false));
        Assert.False(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.PlanarianAdmin, false));

        var clientPermissions = (await new UserRepository(actor.Db, actor.Db.RequestUser)
                .GetPermissions(actor.Db.RequestUser.Id, world.AccountId))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(PermissionPolicyKey.Admin, clientPermissions);
        Assert.Contains(PermissionPolicyKey.AdminManager, clientPermissions);
        Assert.Contains(PermissionPolicyKey.Export, clientPermissions);
        Assert.DoesNotContain(PermissionPolicyKey.PlanarianAdmin, clientPermissions);
    }

    [Fact]
    public async Task AccountScopedPermissionsNeverCrossTheSelectedAccountBoundary()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(AccountScopedPermissionsNeverCrossTheSelectedAccountBoundary));
        var accountA = await CreateWorldAsync(database);
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'd');
        const string userId = "acctscope1";

        await GrantScopedAsync(database, accountA, userId, "account", PermissionKey.Manager);
        await GrantAccountAdminAsync(database, accountA.AccountId, userId);

        await using var actorB = await CaveTestActor.CreateAsync(database, accountB.AccountId, userId);

        Assert.False(await actorB.Db.RequestUser.HasCavePermission(PermissionPolicyKey.Admin, false));
        Assert.False(await actorB.Db.RequestUser.HasCavePermission(PermissionPolicyKey.Manager, false));
        Assert.False(await actorB.Db.RequestUser.HasCavePermission(PermissionPolicyKey.View, false));
        Assert.False(await actorB.Db.RequestUser.HasCavePermission(
            PermissionPolicyKey.Manager,
            accountB.CaveId,
            accountB.CountyId,
            accountB.StateId,
            false));
    }

    [Fact]
    public async Task DisjointScopedGrantsComposeWithoutBroadeningEachOther()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(DisjointScopedGrantsComposeWithoutBroadeningEachOther));
        var world = await CreateWorldAsync(database);
        const string userId = "mixed0001";
        var otherStateCave = world.Caves.Single(cave => cave.StateId != world.Target.StateId);

        await GrantScopedAsync(database, world, userId, "state", PermissionKey.View);
        await GrantScopedAsync(
            database,
            world.AccountId,
            otherStateCave,
            userId,
            "cave",
            PermissionKey.Manager);

        await using var actor = await CaveTestActor.CreateAsync(database, world.AccountId, userId);

        foreach (var cave in world.Caves.Where(cave => cave.StateId == world.Target.StateId))
        {
            Assert.True(await actor.Db.RequestUser.HasCavePermission(
                PermissionPolicyKey.View, cave.CaveId, cave.CountyId, cave.StateId, false));
            Assert.False(await actor.Db.RequestUser.HasCavePermission(
                PermissionPolicyKey.Manager, cave.CaveId, cave.CountyId, cave.StateId, false));
        }

        Assert.True(await actor.Db.RequestUser.HasCavePermission(
            PermissionPolicyKey.Manager,
            otherStateCave.CaveId,
            otherStateCave.CountyId,
            otherStateCave.StateId,
            false));
        Assert.True(await actor.Db.RequestUser.HasCavePermission(
            PermissionPolicyKey.View,
            otherStateCave.CaveId,
            otherStateCave.CountyId,
            otherStateCave.StateId,
            false));
        Assert.False(await actor.Db.RequestUser.HasCavePermission(
            PermissionPolicyKey.Manager, null, otherStateCave.CountyId, null, false));
        Assert.False(await actor.Db.RequestUser.HasCavePermission(
            PermissionPolicyKey.Manager, null, null, otherStateCave.StateId, false));
        Assert.True(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.Manager, false));
    }

    [Fact]
    public async Task PermissionRevocationIsObservedWithoutReinitializingRequestUser()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(PermissionRevocationIsObservedWithoutReinitializingRequestUser));
        var world = await CreateWorldAsync(database);
        const string userId = "revoke001";

        await GrantScopedAsync(database, world, userId, "county", PermissionKey.Manager);
        await using var actor = await CaveTestActor.CreateAsync(database, world.AccountId, userId);

        Assert.True(await actor.Db.RequestUser.HasCavePermission(
            PermissionPolicyKey.Manager,
            world.Target.CaveId,
            world.Target.CountyId,
            world.Target.StateId,
            false));

        await using (var revoke = database.CreateDbContext("revoke", world.AccountId))
        {
            await revoke.CavePermissions
                .Where(row => row.AccountId == world.AccountId && row.UserId == actor.Db.RequestUser.Id)
                .ExecuteDeleteAsync();
        }

        Assert.False(await actor.Db.RequestUser.HasCavePermission(
            PermissionPolicyKey.Manager,
            world.Target.CaveId,
            world.Target.CountyId,
            world.Target.StateId,
            false));
        Assert.False(await actor.Db.RequestUser.HasCavePermission(PermissionPolicyKey.Manager, false));

        var clientPermissions = (await new UserRepository(actor.Db, actor.Db.RequestUser)
                .GetPermissions(actor.Db.RequestUser.Id, world.AccountId))
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(PermissionPolicyKey.Manager, clientPermissions);
        Assert.DoesNotContain(PermissionPolicyKey.AdminManager, clientPermissions);
    }
}
