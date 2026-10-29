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
