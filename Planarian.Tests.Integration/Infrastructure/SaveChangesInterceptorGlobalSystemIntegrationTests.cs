using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Xunit;

namespace Planarian.Tests.Integration.Infrastructure;

public sealed class SaveChangesInterceptorGlobalSystemIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task OrdinaryAccountUserCannotCreateGlobalUserPermission()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(OrdinaryAccountUserCannotCreateGlobalUserPermission));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "globperm01",
            Name = "Global test",
            Description = "Must remain platform-admin only",
            Key = "global-test",
            PermissionType = PermissionType.User
        });

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            attack.UserPermissions.Add(new UserPermission
            {
                Id = "globuser01",
                UserId = attack.RequestUser.Id,
                PermissionId = "globperm01",
                AccountId = null
            });
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.False(await verify.UserPermissions.IgnoreQueryFilters().AnyAsync(row => row.Id == "globuser01"));
    }

    [Fact]
    public async Task OrdinaryAccountUserCannotCreatePermissionDefinition()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(OrdinaryAccountUserCannotCreatePermissionDefinition));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            attack.Permissions.Add(new Permission
            {
                Id = "injectperm1",
                Name = "Injected",
                Description = "Tenant must not create global permission definitions",
                Key = "injected-permission",
                PermissionType = PermissionType.User
            });
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.False(await verify.Permissions.AnyAsync(permission => permission.Id == "injectperm1"));
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task OrdinaryAccountUserCannotModifyOrDeletePermissionDefinition(EntityState state)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(OrdinaryAccountUserCannotModifyOrDeletePermissionDefinition)}-{state}");
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "mutperm001",
            Name = "Protected",
            Description = "Protected global permission definition",
            Key = "protected-permission",
            PermissionType = PermissionType.User
        });

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            var permission = await attack.Permissions.SingleAsync(row => row.Id == "mutperm001");
            if (state == EntityState.Modified)
                permission.Name = "Hijacked";
            else
                attack.Permissions.Remove(permission);

            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.Equal("Protected",
            await verify.Permissions.Where(permission => permission.Id == "mutperm001")
                .Select(permission => permission.Name)
                .SingleAsync());
    }

    [Fact]
    public async Task OrdinaryAccountUserCannotCreateGlobalFeatureSetting()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(OrdinaryAccountUserCannotCreateGlobalFeatureSetting));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            attack.FeatureSettings.Add(new FeatureSetting
            {
                Id = "globfeat01",
                AccountId = null,
                Key = FeatureKey.EnabledFieldCaveId,
                IsEnabled = true,
                IsDefault = true
            });
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.False(await verify.FeatureSettings.IgnoreQueryFilters()
            .AnyAsync(setting => setting.Id == "globfeat01"));
    }

    [Fact]
    public async Task TenantCannotLaunderGlobalFeatureSettingIntoSelectedAccount()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenantCannotLaunderGlobalFeatureSettingIntoSelectedAccount));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await GlobalSystemTestData.AddFeatureSettingAsync(database, new FeatureSetting
        {
            Id = "globfeat01",
            Key = FeatureKey.EnabledFieldCaveId,
            IsEnabled = true,
            IsDefault = true
        });

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            var setting = await attack.FeatureSettings.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == "globfeat01");
            setting.AccountId = account.AccountId;
            setting.IsDefault = false;
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        var persisted = await verify.FeatureSettings.IgnoreQueryFilters()
            .SingleAsync(setting => setting.Id == "globfeat01");
        Assert.Null(persisted.AccountId);
        Assert.True(persisted.IsDefault);
    }

    [Fact]
    public async Task TenantCannotPromoteAccountFeatureSettingToGlobal()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenantCannotPromoteAccountFeatureSettingToGlobal));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');

        await using (var seed = database.CreateDbContext("membera", account.AccountId))
        {
            seed.FeatureSettings.Add(new FeatureSetting
            {
                Id = "acctfeat01",
                AccountId = account.AccountId,
                Key = FeatureKey.EnabledFieldCaveId,
                IsEnabled = true,
                IsDefault = false
            });
            await seed.SaveChangesAsync();
        }

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            var setting = await attack.FeatureSettings.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == "acctfeat01");
            setting.AccountId = null;
            setting.IsDefault = true;
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        var persisted = await verify.FeatureSettings.IgnoreQueryFilters()
            .SingleAsync(setting => setting.Id == "acctfeat01");
        Assert.Equal(account.AccountId, persisted.AccountId);
        Assert.False(persisted.IsDefault);
    }

    [Fact]
    public async Task TenantCannotDeleteGlobalFeatureSetting()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenantCannotDeleteGlobalFeatureSetting));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await GlobalSystemTestData.AddFeatureSettingAsync(database, new FeatureSetting
        {
            Id = "globfeat01",
            Key = FeatureKey.EnabledFieldCaveId,
            IsEnabled = true,
            IsDefault = true
        });

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            var setting = await attack.FeatureSettings.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == "globfeat01");
            attack.FeatureSettings.Remove(setting);
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.True(await verify.FeatureSettings.IgnoreQueryFilters()
            .AnyAsync(setting => setting.Id == "globfeat01"));
    }

    [Fact]
    public async Task OrdinaryAccountUserCannotCreateDefaultGlobalTag()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(OrdinaryAccountUserCannotCreateDefaultGlobalTag));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            attack.TagTypes.Add(new TagType("Injected global", TagTypeKeyConstant.Biology)
            {
                Id = "globaltag1",
                AccountId = null,
                IsDefault = true
            });
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.False(await verify.TagTypes.IgnoreQueryFilters().AnyAsync(row => row.Id == "globaltag1"));
    }

    [Fact]
    public async Task TenantCannotModifyGlobalUserPermission()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenantCannotModifyGlobalUserPermission));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await SeedGlobalUserPermissionAsync(database, account.AccountId, "membera", "globuser01");
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "globperm02",
            Name = "Other global",
            Description = "Alternative protected permission",
            Key = "global-test-2",
            PermissionType = PermissionType.User
        });

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            var row = await attack.UserPermissions.IgnoreQueryFilters()
                .SingleAsync(permission => permission.Id == "globuser01");
            row.PermissionId = "globperm02";
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.Equal("globperm01",
            await verify.UserPermissions.IgnoreQueryFilters()
                .Where(permission => permission.Id == "globuser01")
                .Select(permission => permission.PermissionId)
                .SingleAsync());
    }

    [Fact]
    public async Task TenantCannotModifyGlobalFeatureSetting()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenantCannotModifyGlobalFeatureSetting));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await GlobalSystemTestData.AddFeatureSettingAsync(database, new FeatureSetting
        {
            Id = "globfeat01",
            Key = FeatureKey.EnabledFieldCaveId,
            IsEnabled = true,
            IsDefault = true
        });

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            var setting = await attack.FeatureSettings.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == "globfeat01");
            setting.IsEnabled = false;
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.True(await verify.FeatureSettings.IgnoreQueryFilters()
            .Where(setting => setting.Id == "globfeat01")
            .Select(setting => setting.IsEnabled)
            .SingleAsync());
    }

    [Fact]
    public async Task TenantCannotModifyDefaultGlobalTag()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenantCannotModifyDefaultGlobalTag));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var tag = await ReferenceTestData.AddTagAsync(database, account.AccountId,
            TagTypeKeyConstant.Biology, "Default", "globaltag1", isDefault: true);

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            var row = await attack.TagTypes.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == tag.Id);
            row.Name = "Hijacked";
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.Equal("Default",
            await verify.TagTypes.IgnoreQueryFilters()
                .Where(candidate => candidate.Id == tag.Id)
                .Select(candidate => candidate.Name)
                .SingleAsync());
    }

    [Fact]
    public async Task TenantCannotLaunderGlobalUserPermissionIntoSelectedAccount()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenantCannotLaunderGlobalUserPermissionIntoSelectedAccount));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await SeedGlobalUserPermissionAsync(database, account.AccountId, "membera", "globuser01");

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            var row = await attack.UserPermissions.IgnoreQueryFilters()
                .SingleAsync(permission => permission.Id == "globuser01");
            row.AccountId = account.AccountId;
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.Null(await verify.UserPermissions.IgnoreQueryFilters()
            .Where(permission => permission.Id == "globuser01")
            .Select(permission => permission.AccountId)
            .SingleAsync());
    }

    [Fact]
    public async Task TenantCannotPromoteAccountUserPermissionToGlobal()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenantCannotPromoteAccountUserPermissionToGlobal));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "acctperm01",
            Name = "Account permission",
            Description = "Tenant-scoped permission",
            Key = "account-test",
            PermissionType = PermissionType.User
        });

        await using (var seed = database.CreateDbContext("membera", account.AccountId))
        {
            seed.UserPermissions.Add(new UserPermission
            {
                Id = "acctuser01",
                UserId = seed.RequestUser.Id,
                PermissionId = "acctperm01",
                AccountId = account.AccountId
            });
            await seed.SaveChangesAsync();
        }

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            var row = await attack.UserPermissions.IgnoreQueryFilters()
                .SingleAsync(permission => permission.Id == "acctuser01");
            row.AccountId = null;
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.Equal(account.AccountId,
            await verify.UserPermissions.IgnoreQueryFilters()
                .Where(permission => permission.Id == "acctuser01")
                .Select(permission => permission.AccountId)
                .SingleAsync());
    }

    [Fact]
    public async Task TenantCannotDeleteGlobalUserPermission()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(TenantCannotDeleteGlobalUserPermission));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await SeedGlobalUserPermissionAsync(database, account.AccountId, "membera", "globuser01");

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            var row = await attack.UserPermissions.IgnoreQueryFilters()
                .SingleAsync(permission => permission.Id == "globuser01");
            attack.UserPermissions.Remove(row);
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.True(await verify.UserPermissions.IgnoreQueryFilters()
            .AnyAsync(permission => permission.Id == "globuser01"));
    }

    [Fact]
    public async Task TenantCannotLaunderDefaultGlobalTagIntoSelectedAccount()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenantCannotLaunderDefaultGlobalTagIntoSelectedAccount));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var tag = await ReferenceTestData.AddTagAsync(database, account.AccountId,
            TagTypeKeyConstant.Biology, "Default", "globaltag1", isDefault: true);

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            var row = await attack.TagTypes.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == tag.Id);
            row.AccountId = account.AccountId;
            row.IsDefault = false;
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        var persisted = await verify.TagTypes.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == tag.Id);
        Assert.Null(persisted.AccountId);
        Assert.True(persisted.IsDefault);
    }

    [Fact]
    public async Task TenantCannotPromoteAccountTagToDefaultGlobal()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenantCannotPromoteAccountTagToDefaultGlobal));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var tag = await ReferenceTestData.AddTagAsync(database, account.AccountId,
            TagTypeKeyConstant.Biology, "Account tag", "accounttag1");

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            var row = await attack.TagTypes.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == tag.Id);
            row.AccountId = null;
            row.IsDefault = true;
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        var persisted = await verify.TagTypes.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == tag.Id);
        Assert.Equal(account.AccountId, persisted.AccountId);
        Assert.False(persisted.IsDefault);
    }

    [Fact]
    public async Task TenantCannotDeleteDefaultGlobalTag()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(TenantCannotDeleteDefaultGlobalTag));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var tag = await ReferenceTestData.AddTagAsync(database, account.AccountId,
            TagTypeKeyConstant.Biology, "Default", "globaltag1", isDefault: true);

        await using (var attack = database.CreateDbContext("membera", account.AccountId))
        {
            var row = await attack.TagTypes.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == tag.Id);
            attack.TagTypes.Remove(row);
            await AssertForbiddenAsync(attack);
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.True(await verify.TagTypes.IgnoreQueryFilters().AnyAsync(candidate => candidate.Id == tag.Id));
    }

    [Fact]
    public async Task PlatformAdminCanCreatePermissionDefinition()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PlatformAdminCanCreatePermissionDefinition));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        const string adminId = "plnadmin01";
        await AddPlatformAdminAsync(database, account.AccountId, adminId);

        await using (var admin = database.CreateDbContext(adminId, null))
        {
            await admin.RequestUser.Initialize(null, adminId);
            admin.Permissions.Add(new Permission
            {
                Id = "adminperm1",
                Name = "Admin-created",
                Description = "Created by platform admin",
                Key = "admin-created",
                PermissionType = PermissionType.User
            });
            await admin.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.True(await verify.Permissions.AnyAsync(permission => permission.Id == "adminperm1"));
    }

    [Fact]
    public async Task PlatformAdminCanModifyGlobalFeatureSetting()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PlatformAdminCanModifyGlobalFeatureSetting));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        const string adminId = "plnadmin01";
        await AddPlatformAdminAsync(database, account.AccountId, adminId);
        await GlobalSystemTestData.AddFeatureSettingAsync(database, new FeatureSetting
        {
            Id = "globfeat01",
            Key = FeatureKey.EnabledFieldCaveId,
            IsEnabled = true,
            IsDefault = true
        });

        await using (var admin = database.CreateDbContext(adminId, null))
        {
            await admin.RequestUser.Initialize(null, adminId);
            var setting = await admin.FeatureSettings.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == "globfeat01");
            setting.IsEnabled = false;
            await admin.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.False(await verify.FeatureSettings.IgnoreQueryFilters()
            .Where(setting => setting.Id == "globfeat01")
            .Select(setting => setting.IsEnabled)
            .SingleAsync());
    }

    [Fact]
    public async Task PlatformAdminCanModifyDefaultGlobalTag()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(PlatformAdminCanModifyDefaultGlobalTag));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        const string adminId = "plnadmin01";
        await AddPlatformAdminAsync(database, account.AccountId, adminId);
        var tag = await ReferenceTestData.AddTagAsync(database, account.AccountId,
            TagTypeKeyConstant.Biology, "Default", "globaltag1", isDefault: true);

        await using (var admin = database.CreateDbContext(adminId, null))
        {
            await admin.RequestUser.Initialize(null, adminId);
            var row = await admin.TagTypes.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == tag.Id);
            row.Name = "Updated by platform admin";
            await admin.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.Equal("Updated by platform admin",
            await verify.TagTypes.IgnoreQueryFilters()
                .Where(candidate => candidate.Id == tag.Id)
                .Select(candidate => candidate.Name)
                .SingleAsync());
    }

    [Fact]
    public async Task PlatformAdminCanCreateGlobalUserPermission()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(PlatformAdminCanCreateGlobalUserPermission));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        const string adminId = "plnadmin01";
        const string targetUserId = "targetusr1";
        await AddPlatformAdminAsync(database, account.AccountId, adminId);
        await using var targetUserContext = database.CreateDbContext(targetUserId, account.AccountId);

        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "globperm02",
            Name = "Global extra",
            Description = "Platform-admin assignable global permission",
            Key = "global-extra",
            PermissionType = PermissionType.User
        });

        await using (var admin = database.CreateDbContext(adminId, null))
        {
            await admin.RequestUser.Initialize(null, adminId);
            admin.UserPermissions.Add(new UserPermission
            {
                Id = "globuser02",
                UserId = targetUserId,
                PermissionId = "globperm02",
                AccountId = null
            });
            await admin.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.True(await verify.UserPermissions.IgnoreQueryFilters().AnyAsync(permission =>
            permission.Id == "globuser02" &&
            permission.UserId == targetUserId &&
            permission.AccountId == null));
    }

    private static async Task SeedGlobalUserPermissionAsync(
        PostgresTestDatabase database,
        string accountId,
        string userId,
        string userPermissionId)
    {
        await using var userContext = database.CreateDbContext(userId, accountId);
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "globperm01",
            Name = "Global test",
            Description = "Protected global permission",
            Key = "global-test",
            PermissionType = PermissionType.User
        });

        await GlobalSystemTestData.AddUserPermissionAsync(
            database, userPermissionId, userId, "globperm01");
    }

    private static async Task AddPlatformAdminAsync(
        PostgresTestDatabase database,
        string accountId,
        string adminId)
    {
        await using var adminUserContext = database.CreateDbContext(adminId, accountId);
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "plnadmperm",
            Key = PermissionKey.PlanarianAdmin,
            Name = "Planarian Admin",
            Description = "Platform administration",
            PermissionType = PermissionType.User
        });

        await GlobalSystemTestData.AddUserPermissionAsync(
            database, "plnadmup01", adminId, "plnadmperm");
    }

    private static async Task AssertForbiddenAsync(DbContext db)
    {
        var exception = await Assert.ThrowsAsync<ApiException>(() => db.SaveChangesAsync());
        Assert.Equal(403, exception.StatusCode);
    }
}
