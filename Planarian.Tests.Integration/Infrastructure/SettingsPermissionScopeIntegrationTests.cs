using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Settings.Services;
using Planarian.Shared.Options;
using Planarian.Shared.Services;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;

namespace Planarian.Tests.Integration.Infrastructure;

public sealed class SettingsPermissionScopeIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ManagerRegionalSelectorsMatchAuthoritativeGrantScope()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(ManagerRegionalSelectorsMatchAuthoritativeGrantScope));
        var world = await CavePermissionMatrixTestSupport.CreateWorldAsync(database);
        var otherStateCave = world.Caves.Single(cave => cave.StateId != world.Target.StateId);
        await AddAccountStatesAsync(database, world.AccountId, world.Target.StateId, otherStateCave.StateId);

        for (var scopeIndex = 0; scopeIndex < CavePermissionMatrixTestSupport.Scopes.Length; scopeIndex++)
        {
            var scope = CavePermissionMatrixTestSupport.Scopes[scopeIndex];
            var userId = $"sel{scopeIndex}000001";

            await CavePermissionMatrixTestSupport.GrantScopedAsync(
                database, world, userId, scope, PermissionKey.Manager);

            await using var actor = await CaveTestActor.CreateAsync(database, world.AccountId, userId);
            var service = CreateService(actor);

            var states = (await service.GetStates(PermissionKey.Manager))
                .Select(item => item.Value)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            List<string> expectedStates = scope switch
            {
                "cave" => [],
                "county" or "state" => [world.Target.StateId],
                "account" => new[] { world.Target.StateId, otherStateCave.StateId }
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList(),
                _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
            };
            Assert.Equal(expectedStates, states);

            var targetStateCounties = (await service.GetStateCounties(
                    world.Target.StateId, PermissionKey.Manager))
                .Select(item => item.Value)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            List<string> expectedTargetCounties = scope switch
            {
                "cave" => [],
                "county" => [world.Target.CountyId],
                "state" or "account" => world.Caves
                    .Where(cave => cave.StateId == world.Target.StateId)
                    .Select(cave => cave.CountyId)
                    .Distinct()
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList(),
                _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
            };
            Assert.Equal(expectedTargetCounties, targetStateCounties);
        }
    }

    [Fact]
    public async Task AccountAdminGetsAllRegionalChoicesWhileUnfilteredCallersRemainUnchanged()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(AccountAdminGetsAllRegionalChoicesWhileUnfilteredCallersRemainUnchanged));
        var world = await CavePermissionMatrixTestSupport.CreateWorldAsync(database);
        var otherStateCave = world.Caves.Single(cave => cave.StateId != world.Target.StateId);
        await AddAccountStatesAsync(database, world.AccountId, world.Target.StateId, otherStateCave.StateId);

        const string adminId = "seladmin01";
        await CavePermissionMatrixTestSupport.GrantAccountAdminAsync(database, world.AccountId, adminId);
        await using var admin = await CaveTestActor.CreateAsync(database, world.AccountId, adminId);
        var adminService = CreateService(admin);

        var expectedAllStates = new[] { world.Target.StateId, otherStateCave.StateId }
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(expectedAllStates,
            (await adminService.GetStates(PermissionKey.Manager))
            .Select(item => item.Value)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList());

        const string caveManagerId = "selcave001";
        await CavePermissionMatrixTestSupport.GrantScopedAsync(
            database, world, caveManagerId, "cave", PermissionKey.Manager);
        await using var caveManager = await CaveTestActor.CreateAsync(database, world.AccountId, caveManagerId);
        var caveManagerService = CreateService(caveManager);

        Assert.Empty(await caveManagerService.GetStates(PermissionKey.Manager));
        Assert.Equal(expectedAllStates,
            (await caveManagerService.GetStates())
            .Select(item => item.Value)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList());
    }

    [Fact]
    public async Task CountyManagerCanSelectAnEmptyCounty()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(CountyManagerCanSelectAnEmptyCounty));
        var world = await CavePermissionMatrixTestSupport.CreateWorldAsync(database);
        await AddAccountStatesAsync(database, world.AccountId, world.Target.StateId);
        const string emptyCountyId = "emptycty01";

        await using (var seed = database.CreateDbContext("emptyseed", world.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = emptyCountyId,
                AccountId = world.AccountId,
                StateId = world.Target.StateId,
                DisplayId = "EMPTY",
                Name = "Empty County"
            });
            await seed.SaveChangesAsync();
        }

        const string managerId = "emptymgr01";
        await CavePermissions.GrantCountyManagerAsync(
            database, world.AccountId, emptyCountyId, managerId);
        await using var manager = await CaveTestActor.CreateAsync(database, world.AccountId, managerId);
        var service = CreateService(manager);

        Assert.Contains((await service.GetStates(PermissionKey.Manager)).Select(item => item.Value),
            stateId => stateId == world.Target.StateId);
        Assert.Contains((await service.GetStateCounties(world.Target.StateId, PermissionKey.Manager))
                .Select(item => item.Value),
            countyId => countyId == emptyCountyId);
    }

    [Fact]
    public async Task MalformedCrossFamilyPermissionsDoNotGrantRegionalManagerChoices()
    {
        await using var database =
            await fixture.CreateDatabaseAsync(nameof(MalformedCrossFamilyPermissionsDoNotGrantRegionalManagerChoices));
        var world = await CavePermissionMatrixTestSupport.CreateWorldAsync(database);
        var otherStateCave = world.Caves.Single(cave => cave.StateId != world.Target.StateId);
        await AddAccountStatesAsync(database, world.AccountId, world.Target.StateId, otherStateCave.StateId);
        const string userId = "badselect1";
        const string userManagerPermissionId = "badmgr001";
        const string caveAdminPermissionId = "badadm001";

        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = userManagerPermissionId,
            Key = PermissionKey.Manager,
            Name = "Wrong Type Manager",
            Description = "Regression-only User permission with a Cave Manager key.",
            PermissionType = PermissionType.User
        });
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = caveAdminPermissionId,
            Key = PermissionKey.Admin,
            Name = "Wrong Type Admin",
            Description = "Regression-only Cave permission with an Account Admin key.",
            PermissionType = PermissionType.Cave
        });

        await using (var seed = database.CreateDbContext(userId, world.AccountId))
        {
            await CavePermissions.EnsureAccountUserAsync(seed, world.AccountId);
            seed.CavePermissions.Add(new CavePermission
            {
                UserId = seed.RequestUser.Id,
                AccountId = world.AccountId,
                PermissionId = userManagerPermissionId
            });
            seed.UserPermissions.Add(new UserPermission
            {
                UserId = seed.RequestUser.Id,
                AccountId = world.AccountId,
                PermissionId = caveAdminPermissionId
            });
            await seed.SaveChangesAsync();
        }

        await using var actor = await CaveTestActor.CreateAsync(database, world.AccountId, userId);
        var service = CreateService(actor);

        Assert.Empty(await service.GetStates(PermissionKey.Manager));
        Assert.Empty(await service.GetStateCounties(world.Target.StateId, PermissionKey.Manager));
    }

    private static SettingsService CreateService(CaveTestActor actor) =>
        new(
            new SettingsRepository(actor.Db, actor.Db.RequestUser),
            actor.Db.RequestUser,
            new FixedApiRequestOrigin(),
            new RequestThrottleOptions());

    private static async Task AddAccountStatesAsync(
        PostgresTestDatabase database,
        string accountId,
        params string[] stateIds)
    {
        await using var db = database.CreateDbContext("statesseed", accountId);
        foreach (var stateId in stateIds.Distinct(StringComparer.Ordinal))
        {
            if (!await db.AccountStates.AnyAsync(row => row.AccountId == accountId && row.StateId == stateId))
                db.AccountStates.Add(new AccountState { AccountId = accountId, StateId = stateId });
        }
        await db.SaveChangesAsync();
    }

    private sealed class FixedApiRequestOrigin : IApiRequestOrigin
    {
        public string GetOrigin() => "https://planarian.integration.test";
    }
}
