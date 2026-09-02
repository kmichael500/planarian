using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Account.Model;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

namespace Planarian.Tests;

public sealed class AccountSettingsMutationIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task UpdateMiscSettingsPersistsAccountAndStateChangesTogether()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(UpdateMiscSettingsPersistsAccountAndStateChangesTogether));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a', addAccountState: true);
        const string otherStateId = "state0000z";
        await GlobalStateTestData.AddAsync(database, otherStateId, "State Z", "ZZ");

        await using (var db = database.CreateDbContext("account-admin", account.AccountId))
            await IntegrationTestServices.For(db).Account.UpdateMiscAccountSettingsVm(new MiscAccountSettingsVm
            {
                AccountName = "Updated Account",
                CountyIdDelimiter = "/",
                StateIds = [otherStateId],
                DefaultViewAccessAllCaves = true,
                ExportEnabled = false
            }, default);

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        var persisted = await verify.Accounts.SingleAsync(row => row.Id == account.AccountId);
        Assert.Equal("Updated Account", persisted.Name);
        Assert.Equal("/", persisted.CountyIdDelimiter);
        Assert.True(persisted.DefaultViewAccessAllCaves);
        Assert.False(persisted.ExportEnabled);
        Assert.Equal([otherStateId], await verify.AccountStates
            .Where(row => row.AccountId == account.AccountId)
            .Select(row => row.StateId).ToListAsync());
    }

    [Fact]
    public async Task InvalidStateRemovalDoesNotPersistAnySettingsChanges()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InvalidStateRemovalDoesNotPersistAnySettingsChanges));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string otherStateId = "state0000z";
        await GlobalStateTestData.AddAsync(database, otherStateId, "State Z", "ZZ");
        await using (var seed = database.CreateDbContext("account-state-seed", cave.AccountId))
        {
            seed.AccountStates.Add(new AccountState
            {
                Id = "acctstatea", AccountId = cave.AccountId, StateId = cave.StateId
            });
            await seed.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext("account-admin", cave.AccountId))
        {
            var exception = await Assert.ThrowsAsync<ApiException>(() => IntegrationTestServices.For(db).Account
                .UpdateMiscAccountSettingsVm(new MiscAccountSettingsVm
                {
                    AccountName = "Should Not Persist",
                    CountyIdDelimiter = "/",
                    StateIds = [otherStateId],
                    DefaultViewAccessAllCaves = true,
                    ExportEnabled = false
                }, default));
            Assert.Equal(400, exception.StatusCode);
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var persisted = await verify.Accounts.SingleAsync(row => row.Id == cave.AccountId);
        Assert.Equal("Account A", persisted.Name);
        Assert.Equal("-", persisted.CountyIdDelimiter);
        Assert.False(persisted.DefaultViewAccessAllCaves);
        Assert.True(persisted.ExportEnabled);
        Assert.Equal([cave.StateId], await verify.AccountStates
            .Where(row => row.AccountId == cave.AccountId)
            .Select(row => row.StateId).ToListAsync());
        Assert.False(await verify.AccountStates.AnyAsync(row =>
            row.AccountId == cave.AccountId && row.StateId == otherStateId));
    }
}
