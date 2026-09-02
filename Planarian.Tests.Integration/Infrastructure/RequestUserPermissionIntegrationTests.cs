using Microsoft.EntityFrameworkCore;
using Planarian.Library.Options;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.App.Repositories;
using Planarian.Modules.App.Services;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Services;
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

    [Fact]
    public async Task GlobalPlanarianAdminDoesNotInheritAccountOrCavePermissions()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(GlobalPlanarianAdminDoesNotInheritAccountOrCavePermissions));
        var tenant = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var cave = await CaveTestDataFactory.AddCaveAsync(database, tenant);
        const string userId = "plnadmin01";
        await AddPlatformAdminToAccountAsync(database, tenant.AccountId, userId);

        await using var db = database.CreateDbContext(userId, tenant.AccountId);
        await db.RequestUser.Initialize(tenant.AccountId, userId);

        Assert.True(await db.RequestUser.HasCavePermission(PermissionPolicyKey.PlanarianAdmin, false));
        Assert.False(await db.RequestUser.HasCavePermission(PermissionPolicyKey.Admin, false));
        Assert.False(await db.RequestUser.HasCavePermission(PermissionPolicyKey.Manager, false));
        Assert.False(await db.RequestUser.HasCavePermission(PermissionPolicyKey.View, false));
        Assert.False(await db.RequestUser.HasCavePermission(PermissionPolicyKey.AdminManager, false));
        Assert.False(await db.RequestUser.HasCavePermission(PermissionPolicyKey.Export, false));
        Assert.False(await db.RequestUser.HasCavePermission(
            PermissionPolicyKey.View, cave.CaveId, cave.CountyId, cave.StateId, false));
        Assert.Equal([PermissionPolicyKey.PlanarianAdmin], (await CreateAppService(db).Initialize()).Permissions);
    }

    [Fact]
    public async Task AccountAdminStillInheritsAccountCapabilitiesWithoutPlatformAdministration()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AccountAdminStillInheritsAccountCapabilitiesWithoutPlatformAdministration));
        var tenant = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var cave = await CaveTestDataFactory.AddCaveAsync(database, tenant);
        const string userId = "acctadmin1";
        await AddAccountAdminAsync(database, tenant.AccountId, userId);

        await using var db = database.CreateDbContext(userId, tenant.AccountId);
        await db.RequestUser.Initialize(tenant.AccountId, userId);

        Assert.True(await db.RequestUser.HasCavePermission(PermissionPolicyKey.Admin, false));
        Assert.True(await db.RequestUser.HasCavePermission(PermissionPolicyKey.Manager, false));
        Assert.True(await db.RequestUser.HasCavePermission(PermissionPolicyKey.View, false));
        Assert.True(await db.RequestUser.HasCavePermission(PermissionPolicyKey.AdminManager, false));
        Assert.True(await db.RequestUser.HasCavePermission(PermissionPolicyKey.Export, false));
        Assert.False(await db.RequestUser.HasCavePermission(PermissionPolicyKey.PlanarianAdmin, false));
        Assert.True(await db.RequestUser.HasCavePermission(
            PermissionPolicyKey.Manager, cave.CaveId, cave.CountyId, cave.StateId, false));
        var appPermissions = (await CreateAppService(db).Initialize()).Permissions;
        Assert.Contains(PermissionPolicyKey.Admin, appPermissions);
        Assert.Contains(PermissionPolicyKey.AdminManager, appPermissions);
        Assert.Contains(PermissionPolicyKey.Export, appPermissions);
        Assert.DoesNotContain(PermissionPolicyKey.PlanarianAdmin, appPermissions);
    }

    [Fact]
    public async Task AppInitializationUsesResolvedDefaultAccountForAccountCapabilities()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AppInitializationUsesResolvedDefaultAccountForAccountCapabilities));
        var tenant = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        const string userId = "acctadmin1";
        await AddAccountAdminAsync(database, tenant.AccountId, userId);

        await using var db = database.CreateDbContext(userId, null);
        await db.RequestUser.Initialize(null, userId);
        Assert.Null(db.RequestUser.AccountId);

        var result = await CreateAppService(db).Initialize();

        Assert.Equal(tenant.AccountId, result.CurrentUser!.CurrentAccountId);
        Assert.Contains(PermissionPolicyKey.Admin, result.Permissions);
        Assert.Contains(PermissionPolicyKey.AdminManager, result.Permissions);
        Assert.Contains(PermissionPolicyKey.Export, result.Permissions);
    }

    [Fact]
    public async Task AppInitializationUsesResolvedDefaultAccountExportSettingForMember()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AppInitializationUsesResolvedDefaultAccountExportSettingForMember));
        var tenant = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        const string userId = "exportuser";

        await using (var seed = database.CreateDbContext(userId, tenant.AccountId))
        {
            seed.AccountUsers.Add(new AccountUser { AccountId = tenant.AccountId, UserId = userId });
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateDbContext(userId, null);
        await db.RequestUser.Initialize(null, userId);
        Assert.Null(db.RequestUser.AccountId);

        var result = await CreateAppService(db).Initialize();

        Assert.Equal(tenant.AccountId, result.CurrentUser!.CurrentAccountId);
        Assert.Contains(PermissionPolicyKey.Export, result.Permissions);
        Assert.DoesNotContain(PermissionPolicyKey.Admin, result.Permissions);
    }

    [Fact]
    public async Task AppInitializationExposesPlatformAdminWithoutSelectedAccount()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AppInitializationExposesPlatformAdminWithoutSelectedAccount));
        const string userId = "plnadmin01";
        await AddGlobalPlanarianAdminAsync(database, userId);

        await using var db = database.CreateDbContext(userId, null);
        await db.RequestUser.Initialize(null, userId);

        var result = await CreateAppService(db).Initialize();

        Assert.Null(result.CurrentUser!.CurrentAccountId);
        Assert.Empty(result.AccountIds);
        Assert.Equal([PermissionPolicyKey.PlanarianAdmin], result.Permissions);
    }

    private static async Task AddPlatformAdminToAccountAsync(PostgresTestDatabase database, string accountId,
        string userId)
    {
        await AddGlobalPlanarianAdminAsync(database, userId);
        await using var seed = database.CreateDbContext(userId, accountId);
        seed.AccountUsers.Add(new AccountUser { AccountId = accountId, UserId = userId });
        var account = await seed.Accounts.SingleAsync(row => row.Id == accountId);
        account.ExportEnabled = false;
        await seed.SaveChangesAsync();
    }

    private static async Task AddGlobalPlanarianAdminAsync(PostgresTestDatabase database, string userId)
    {
        await using var seed = database.CreateDbContext(userId, null);
        seed.Users.Add(new User("Planarian", "Admin", $"{userId}@integration.test") { Id = userId });
        seed.Permissions.Add(new Permission
        {
            Id = "plnadmperm", Key = PermissionKey.PlanarianAdmin, Name = "Planarian Admin",
            Description = "Global Planarian administration", PermissionType = PermissionType.User
        });
        seed.UserPermissions.Add(new UserPermission { UserId = userId, PermissionId = "plnadmperm", AccountId = null });
        await seed.SaveChangesAsync();
    }

    private static async Task AddAccountAdminAsync(PostgresTestDatabase database, string accountId, string userId)
    {
        await using var seed = database.CreateDbContext(userId, accountId);
        seed.AccountUsers.Add(new AccountUser { AccountId = accountId, UserId = userId });
        seed.Permissions.AddRange(
            new Permission
            {
                Id = "acctadmprm", Key = PermissionKey.Admin, Name = "Admin",
                Description = "Account administration", PermissionType = PermissionType.User
            },
            new Permission
            {
                Id = "cavemgrprm", Key = PermissionKey.Manager, Name = "Manager",
                Description = "Cave management", PermissionType = PermissionType.Cave
            },
            new Permission
            {
                Id = "caveviewpm", Key = PermissionKey.View, Name = "View",
                Description = "Cave viewing", PermissionType = PermissionType.Cave
            });
        seed.UserPermissions.Add(new UserPermission { UserId = userId, PermissionId = "acctadmprm", AccountId = accountId });
        var account = await seed.Accounts.SingleAsync(row => row.Id == accountId);
        account.ExportEnabled = false;
        await seed.SaveChangesAsync();
    }

    private static AppService CreateAppService(PlanarianDbContext db) =>
        new(new AppRepository(db, db.RequestUser), db.RequestUser, new FixedApiRequestOrigin(),
            new ServerOptions { SupportName = "Support", SupportEmail = "support@example.test" },
            new UserRepository(db, db.RequestUser));

    private sealed class FixedApiRequestOrigin : IApiRequestOrigin
    {
        public string GetOrigin() => "https://planarian.test";
    }
}
