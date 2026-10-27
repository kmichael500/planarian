using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Account.Repositories;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

namespace Planarian.Tests;

public sealed class FeatureSettingResolutionIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task AccountOverrideReplacesGlobalDefaultInEffectiveSettings()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AccountOverrideReplacesGlobalDefaultInEffectiveSettings));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await AddGlobalDefaultAsync(database, FeatureKey.EnabledFieldCaveId, "default001", true);
        await using var db = database.CreateDbContext("account-admin", account.AccountId);
        db.FeatureSettings.Add(new FeatureSetting
        {
            Id = "override01",
            AccountId = account.AccountId,
            Key = FeatureKey.EnabledFieldCaveId,
            IsEnabled = false,
            IsDefault = false
        });
        await db.SaveChangesAsync();

        var repository = new FeatureSettingRepository(db, db.RequestUser);
        var effective = (await repository.GetFeatureSettings(default))
            .Where(setting => setting.Key == FeatureKey.EnabledFieldCaveId).ToList();

        var setting = Assert.Single(effective);
        Assert.Equal("override01", setting.Id);
        Assert.False(setting.IsEnabled);
        Assert.False(setting.IsDefault);

        await IntegrationTestServices.For(db).Account.UpdateFeatureSetting(
            FeatureKey.EnabledFieldCaveId, true, default);

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.True((await verify.FeatureSettings.SingleAsync(row => row.Id == "override01")).IsEnabled);
        Assert.True((await verify.FeatureSettings.SingleAsync(row => row.Id == "default001")).IsEnabled);
    }

    [Fact]
    public async Task GlobalDefaultRemainsEffectiveAndImmutableWithoutAccountOverride()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(GlobalDefaultRemainsEffectiveAndImmutableWithoutAccountOverride));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await AddGlobalDefaultAsync(database, FeatureKey.EnabledFieldCaveId, "default001", true);
        await using var db = database.CreateDbContext("account-admin", account.AccountId);

        var repository = new FeatureSettingRepository(db, db.RequestUser);
        var setting = Assert.Single((await repository.GetFeatureSettings(default))
            .Where(candidate => candidate.Key == FeatureKey.EnabledFieldCaveId));
        Assert.True(setting.IsDefault);

        var failure = await Assert.ThrowsAsync<ApiException>(() => IntegrationTestServices.For(db).Account
            .UpdateFeatureSetting(FeatureKey.EnabledFieldCaveId, false, default));
        Assert.Equal(403, failure.StatusCode);
    }

    private static async Task AddGlobalDefaultAsync(PostgresTestDatabase database, FeatureKey key, string id,
        bool isEnabled)
    {
        await GlobalSystemTestData.AddFeatureSettingAsync(database, new FeatureSetting
        {
            Id = id,
            Key = key,
            IsEnabled = isEnabled,
            IsDefault = true
        });
    }
}
