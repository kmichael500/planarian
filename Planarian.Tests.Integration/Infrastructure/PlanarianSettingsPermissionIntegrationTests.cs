using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.PlanarianSettings.Models;
using Planarian.Modules.PlanarianSettings.Repositories;
using Planarian.Modules.PlanarianSettings.Services;
using Xunit;

namespace Planarian.Tests.Integration.Infrastructure;

public sealed class PlanarianSettingsPermissionIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task PlatformAdminCanLoadGlobalStatesAndCreateAccountWithoutSelectedAccount()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PlatformAdminCanLoadGlobalStatesAndCreateAccountWithoutSelectedAccount));
        const string userId = "plnadmin01";
        const string stateId = "state0000p";
        await GlobalStateTestData.AddAsync(database, stateId, "Platform State", "PS");

        await using (var seed = database.CreateDbContext(userId, null))
        {
            seed.Users.Add(new User("Planarian", "Admin", "platform-admin@integration.test") { Id = userId });
            await seed.SaveChangesAsync();
        }
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "plnadmperm", Key = PermissionKey.PlanarianAdmin, Name = "Planarian Admin",
            Description = "Platform administration", PermissionType = PermissionType.User
        });
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "badadmprm1", Key = PermissionKey.Admin, Name = "Wrong Type Admin",
            Description = "Regression-only Cave permission with the Admin key.", PermissionType = PermissionType.Cave
        });
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "acctadmprm", Key = PermissionKey.Admin, Name = "Admin",
            Description = "Account administration", PermissionType = PermissionType.User
        });
        await GlobalSystemTestData.AddUserPermissionAsync(
            database, "plnadmup01", userId, "plnadmperm");

        await using var db = database.CreateDbContext(userId, null);
        await db.RequestUser.Initialize(null, userId);
        var service = new PlanarianSettingsService(new PlanarianSettingsRepository(db, db.RequestUser), db.RequestUser);

        var states = await service.GetAllStates(default);
        Assert.Contains(states, state => state.Value == stateId && state.Display == "Platform State");

        var accountId = await service.CreateAccount(new CreateAccountVm
        {
            Name = "Created by platform admin",
            CountyIdDelimiter = "-",
            StateIds = [stateId],
            DefaultViewAccessAllCaves = false,
            ExportEnabled = false
        }, default);

        await using var verify = database.CreateDbContext("verify", accountId);
        Assert.True(await verify.AccountUsers.AnyAsync(row => row.AccountId == accountId && row.UserId == userId));
        Assert.True(await verify.UserPermissions.AnyAsync(row =>
            row.AccountId == accountId &&
            row.UserId == userId &&
            row.Permission!.Key == PermissionKey.Admin &&
            row.Permission.PermissionType == PermissionType.User));
        Assert.True(await verify.UserPermissions.AnyAsync(row =>
            row.AccountId == null && row.UserId == userId && row.Permission!.Key == PermissionKey.PlanarianAdmin));
    }
}
