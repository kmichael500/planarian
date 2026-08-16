using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Model;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

namespace Planarian.Tests;

public sealed class AccountTenantIsolationIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task AccountTagAdministrationHidesForeignTagsButAllowsOwnedTagsAndForbidsDefaults()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AccountTagAdministrationHidesForeignTagsButAllowsOwnedTagsAndForbidsDefaults));
        var accountA = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var accountB = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'b');
        var owned = await ReferenceTestData.AddTagAsync(database, accountA.AccountId,
            TagTypeKeyConstant.Biology, "Owned", "biology00a");
        var foreign = await ReferenceTestData.AddTagAsync(database, accountB.AccountId,
            TagTypeKeyConstant.Biology, "Foreign", "biology00b");
        var defaultTag = await ReferenceTestData.AddTagAsync(database, accountA.AccountId,
            TagTypeKeyConstant.Biology, "Default", "biology00c", isDefault: true);

        await using (var db = database.CreateDbContext("account-a-admin", accountA.AccountId))
        {
            var service = IntegrationTestServices.For(db).Account;
            var beforeOwnedCount = await db.TagTypes.IgnoreQueryFilters()
                .CountAsync(tag => tag.AccountId == accountA.AccountId);

            var inaccessible = await Assert.ThrowsAsync<ApiException>(() => service.CreateOrUpdateTagType(
                new CreateEditTagTypeVm { Name = "Attack", Key = TagTypeKeyConstant.Biology }, foreign.Id));
            Assert.Equal(404, inaccessible.StatusCode);
            Assert.Equal(beforeOwnedCount, await db.TagTypes.IgnoreQueryFilters()
                .CountAsync(tag => tag.AccountId == accountA.AccountId));

            await service.CreateOrUpdateTagType(
                new CreateEditTagTypeVm { Name = "Owned updated", Key = TagTypeKeyConstant.Biology }, owned.Id);

            var forbidden = await Assert.ThrowsAsync<ApiException>(() => service.CreateOrUpdateTagType(
                new CreateEditTagTypeVm { Name = "Default attack", Key = TagTypeKeyConstant.Biology },
                defaultTag.Id));
            Assert.Equal(403, forbidden.StatusCode);
        }

        await using var verify = database.CreateDbContext("verify", accountA.AccountId);
        var persistedForeign = await verify.TagTypes.IgnoreQueryFilters().SingleAsync(tag => tag.Id == foreign.Id);
        Assert.Equal("Foreign", persistedForeign.Name);
        Assert.Equal(accountB.AccountId, persistedForeign.AccountId);
        Assert.Equal("Owned updated",
            (await verify.TagTypes.IgnoreQueryFilters().SingleAsync(tag => tag.Id == owned.Id)).Name);
        Assert.Equal("Default",
            (await verify.TagTypes.IgnoreQueryFilters().SingleAsync(tag => tag.Id == defaultTag.Id)).Name);
    }

    [Fact]
    public async Task AccountACannotUpdateAccountBCountyById()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AccountACannotUpdateAccountBCountyById));
        var accountA = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var accountB = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'b');

        await using (var db = database.CreateDbContext("account-a-admin", accountA.AccountId))
        {
            var exception = await Assert.ThrowsAsync<ApiException>(() => IntegrationTestServices.For(db).Account
                .CreateOrUpdateCounty(accountA.StateId,
                    new CreateCountyVm { Name = "Attack", CountyDisplayId = "ZZZ" }, accountB.CountyId, default));
            Assert.Equal(404, exception.StatusCode);
        }

        await using var verify = database.CreateDbContext("verify", accountB.AccountId);
        var county = await verify.Counties.IgnoreQueryFilters().SingleAsync(row => row.Id == accountB.CountyId);
        Assert.Equal(accountB.CountyName, county.Name);
        Assert.Equal(accountB.CountyDisplayId, county.DisplayId);
        Assert.Equal(accountB.StateId, county.StateId);
        Assert.Equal(accountB.AccountId, county.AccountId);
    }

    [Fact]
    public async Task AccountACannotDeleteAccountBCountyById()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AccountACannotDeleteAccountBCountyById));
        var accountA = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var accountB = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'b');

        await using (var db = database.CreateDbContext("account-a-admin", accountA.AccountId))
        {
            var exception = await Assert.ThrowsAsync<ApiException>(() =>
                IntegrationTestServices.For(db).Account.DeleteCounties([accountB.CountyId], default));
            Assert.Equal(404, exception.StatusCode);
        }

        await using var verify = database.CreateDbContext("verify", accountB.AccountId);
        Assert.True(await verify.Counties.IgnoreQueryFilters().AnyAsync(row =>
            row.Id == accountB.CountyId && row.AccountId == accountB.AccountId));
    }

    [Fact]
    public async Task CountyUpdateRejectsDuplicateCodeWithinAccountAndState()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CountyUpdateRejectsDuplicateCodeWithinAccountAndState));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await using (var seed = database.CreateDbContext("seed", account.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = "county00a2", AccountId = account.AccountId, StateId = account.StateId,
                Name = "Second", DisplayId = "A02"
            });
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateDbContext("account-admin", account.AccountId);
        var exception = await Assert.ThrowsAsync<ApiException>(() => IntegrationTestServices.For(db).Account
            .CreateOrUpdateCounty(account.StateId,
                new CreateCountyVm { Name = "Renamed", CountyDisplayId = "a02" }, account.CountyId, default));
        Assert.Equal(400, exception.StatusCode);

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        var county = await verify.Counties.SingleAsync(row => row.Id == account.CountyId);
        Assert.Equal(account.CountyName, county.Name);
        Assert.Equal(account.CountyDisplayId, county.DisplayId);
    }

    [Fact]
    public async Task NewCountyRejectsMissingStateAsControlledBadRequest()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(NewCountyRejectsMissingStateAsControlledBadRequest));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await using var db = database.CreateDbContext("account-admin", account.AccountId);

        var exception = await Assert.ThrowsAsync<ApiException>(() => IntegrationTestServices.For(db).Account
            .CreateOrUpdateCounty("missingstate",
                new CreateCountyVm { Name = "Invalid", CountyDisplayId = "BAD" }, null, default));

        Assert.Equal(400, exception.StatusCode);
        Assert.False(await db.Counties.AnyAsync(row => row.Name == "Invalid"));
    }

    [Fact]
    public async Task InUseCountyCannotMoveStateButUnusedCountyCan()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(InUseCountyCannotMoveStateButUnusedCountyCan));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string otherStateId = "state0000z";
        await GlobalStateTestData.AddAsync(database, otherStateId, "State Z", "ZZ");
        const string unusedCountyId = "county00az";
        await using (var seed = database.CreateDbContext("seed", cave.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = unusedCountyId, AccountId = cave.AccountId, StateId = cave.StateId,
                Name = "Unused", DisplayId = "A99"
            });
            await seed.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext("account-admin", cave.AccountId))
        {
            var service = IntegrationTestServices.For(db).Account;
            var exception = await Assert.ThrowsAsync<ApiException>(() => service.CreateOrUpdateCounty(otherStateId,
                new CreateCountyVm { Name = "Moved cave county", CountyDisplayId = "Z01" }, cave.CountyId, default));
            Assert.Equal(400, exception.StatusCode);

            await service.CreateOrUpdateCounty(otherStateId,
                new CreateCountyVm { Name = "Unused moved", CountyDisplayId = "Z02" }, unusedCountyId, default);
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var used = await verify.Counties.SingleAsync(row => row.Id == cave.CountyId);
        Assert.Equal(cave.StateId, used.StateId);
        Assert.Equal(cave.CountyName, used.Name);
        Assert.Equal(cave.CountyDisplayId, used.DisplayId);
        Assert.Equal(otherStateId, (await verify.Counties.SingleAsync(row => row.Id == unusedCountyId)).StateId);
        var persistedCave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal(persistedCave.StateId, used.StateId);
    }
}
