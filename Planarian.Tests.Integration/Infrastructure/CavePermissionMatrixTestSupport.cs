using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Tests.Integration.Infrastructure.Actors;

namespace Planarian.Tests.Integration.Infrastructure;

internal static class CavePermissionMatrixTestSupport
{
    internal static readonly string[] Scopes = ["cave", "county", "state", "account"];
    internal static readonly string[] GrantedCavePermissions = [PermissionKey.View, PermissionKey.Manager];
    internal static readonly string[] RequestedPermissions =
        [PermissionPolicyKey.View, PermissionPolicyKey.Manager, PermissionPolicyKey.Admin];

    internal static bool Satisfies(string grantedPermission, string requestedPermission) =>
        requestedPermission switch
        {
            PermissionPolicyKey.View =>
                grantedPermission is PermissionKey.View or PermissionKey.Manager,
            PermissionPolicyKey.Manager => grantedPermission == PermissionKey.Manager,
            PermissionPolicyKey.Admin => false,
            _ => false
        };

    internal static string MatrixUserId(string scope, string permission) =>
        $"m{ScopeCode(scope)}{permission[0]}00000";

    internal static string ScopeCode(string scope) => scope switch
    {
        "cave" => "cv",
        "county" => "ct",
        "state" => "st",
        "account" => "ac",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
    };

    internal static int ScopeRank(string scope) => scope switch
    {
        "cave" => 0,
        "county" => 1,
        "state" => 2,
        "account" => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
    };

    internal static Task<bool> HasScopePermissionAsync(
        CaveTestActor actor, PermissionCave target, string scope, string permissionKey) =>
        scope switch
        {
            "cave" => actor.Db.RequestUser.HasCavePermission(
                permissionKey, target.CaveId, null, null, false),
            "county" => actor.Db.RequestUser.HasCavePermission(
                permissionKey, null, target.CountyId, null, false),
            "state" => actor.Db.RequestUser.HasCavePermission(
                permissionKey, null, null, target.StateId, false),
            "account" => actor.Db.RequestUser.HasCavePermission(
                permissionKey, null, null, null, false),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };

    internal static HashSet<string> ExpectedCaves(PermissionWorld world, string scope) =>
        scope switch
        {
            "cave" => [world.Target.CaveId],
            "county" => world.Caves
                .Where(cave => cave.CountyId == world.Target.CountyId)
                .Select(cave => cave.CaveId)
                .ToHashSet(StringComparer.Ordinal),
            "state" => world.Caves
                .Where(cave => cave.StateId == world.Target.StateId)
                .Select(cave => cave.CaveId)
                .ToHashSet(StringComparer.Ordinal),
            "account" => world.Caves.Select(cave => cave.CaveId).ToHashSet(StringComparer.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };

    internal static Task GrantScopedAsync(
        PostgresTestDatabase database,
        PermissionWorld world,
        string userId,
        string scope,
        string permissionKey) =>
        GrantScopedAsync(database, world.AccountId, world.Target, userId, scope, permissionKey);

    internal static async Task GrantScopedAsync(
        PostgresTestDatabase database,
        string accountId,
        PermissionCave target,
        string userId,
        string scope,
        string permissionKey)
    {
        var permissionId = permissionKey == PermissionKey.View ? "matrixview" : "matrixmgr0";
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = permissionId,
            Key = permissionKey,
            Name = permissionKey,
            Description = $"{permissionKey} Caves",
            PermissionType = PermissionType.Cave
        });

        await using var db = database.CreateDbContext(userId, accountId);
        await CavePermissions.EnsureAccountUserAsync(db, accountId);
        db.CavePermissions.Add(new CavePermission
        {
            UserId = db.RequestUser.Id,
            AccountId = accountId,
            PermissionId = permissionId,
            CaveId = scope == "cave" ? target.CaveId : null,
            CountyId = scope == "county" ? target.CountyId : null,
            StateId = scope == "state" ? target.StateId : null
        });
        await db.SaveChangesAsync();
    }

    internal static async Task GrantAccountAdminAsync(
        PostgresTestDatabase database,
        string accountId,
        string userId)
    {
        const string permissionId = "matrixadm0";
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = permissionId,
            Key = PermissionKey.Admin,
            Name = PermissionKey.Admin,
            Description = "Account administration",
            PermissionType = PermissionType.User
        });

        await using var db = database.CreateDbContext(userId, accountId);
        await CavePermissions.EnsureAccountUserAsync(db, accountId);
        db.UserPermissions.Add(new UserPermission
        {
            UserId = db.RequestUser.Id,
            AccountId = accountId,
            PermissionId = permissionId
        });
        await db.SaveChangesAsync();
    }

    internal static async Task SetExportEnabledAsync(
        PostgresTestDatabase database,
        string accountId,
        bool enabled)
    {
        await using var db = database.CreateDbContext("matrixseed", accountId);
        await db.Accounts.Where(row => row.Id == accountId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ExportEnabled, enabled));
    }

    internal static async Task<PermissionWorld> CreateWorldAsync(PostgresTestDatabase database)
    {
        var target = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var targetCave = new PermissionCave(target.CaveId, target.CountyId, target.StateId);

        var sameCounty = await CaveTestDataFactory.AddCaveAsync(
            database, target, "cave00000b", "Same County Cave", 2);

        var sameStateCounty = new AccountCountyTestData(
            target.AccountId, target.StateId, target.StateName, target.StateAbbreviation,
            "county000b", "Second County", "AA02");
        await AddCountyAsync(database, sameStateCounty);
        var sameState = await CaveTestDataFactory.AddCaveAsync(
            database, sameStateCounty, "cave00000c", "Same State Cave", 1);

        const string otherStateId = "state0000z";
        await GlobalStateTestData.AddAsync(database, otherStateId, "State Z", "ZZ");
        var otherStateCounty = new AccountCountyTestData(
            target.AccountId, otherStateId, "State Z", "ZZ",
            "county000z", "Other State County", "ZZ01");
        await AddCountyAsync(database, otherStateCounty);
        var otherState = await CaveTestDataFactory.AddCaveAsync(
            database, otherStateCounty, "cave00000z", "Other State Cave", 1);

        return new PermissionWorld(target.AccountId, targetCave,
        [
            targetCave,
            new PermissionCave(sameCounty.CaveId, sameCounty.CountyId, sameCounty.StateId),
            new PermissionCave(sameState.CaveId, sameState.CountyId, sameState.StateId),
            new PermissionCave(otherState.CaveId, otherState.CountyId, otherState.StateId)
        ]);
    }

    private static async Task AddCountyAsync(
        PostgresTestDatabase database,
        AccountCountyTestData county)
    {
        await using var db = database.CreateDbContext("countyseed", county.AccountId);
        db.Counties.Add(new County
        {
            Id = county.CountyId,
            AccountId = county.AccountId,
            StateId = county.StateId,
            DisplayId = county.CountyDisplayId,
            Name = county.CountyName
        });
        await db.SaveChangesAsync();
    }

    internal sealed record PermissionCave(string CaveId, string CountyId, string StateId);

    internal sealed record PermissionWorld(
        string AccountId,
        PermissionCave Target,
        IReadOnlyList<PermissionCave> Caves);
}
