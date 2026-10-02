using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Planarian.Library.Exceptions;
using Planarian.Library.Options;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Account.Services;
using Planarian.Modules.App.Repositories;
using Planarian.Modules.App.Services;
using Planarian.Modules.Authentication.Repositories;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Users;

public sealed class AccountAccessRevocationTests
{
    private const string AccountId = "account001";
    private const string OtherAccountId = "account002";
    private const string TargetUserId = "target0001";
    private const string AdminUserId = "admin00001";

    [Fact]
    public async Task RevokedMembershipCannotSelectAccountButUserRemainsAuthenticated()
    {
        await using var context = CreateContext();
        var seedRequestUser = SetRequestUser(context, TargetUserId, AccountId);
        await SeedUserAndMembership(context, seedRequestUser, DateTime.UtcNow);
        var bootstrapRequestUser = new RequestUser(context);
        await bootstrapRequestUser.Initialize(AccountId, TargetUserId, throwOnInvalidAccountId: false);

        Assert.True(bootstrapRequestUser.IsAuthenticated);
        Assert.Null(bootstrapRequestUser.AccountId);

        var normalRequestUser = new RequestUser(context);
        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            normalRequestUser.Initialize(AccountId, TargetUserId));

        Assert.Equal(StatusCodes.Status401Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task AppInitializationSeparatesRevokedAccountsAndSelectsActiveDefault()
    {
        await using var context = CreateContext();
        var seedRequestUser = SetRequestUser(context, TargetUserId, AccountId);
        var revokedOn = DateTime.UtcNow;

        context.Users.Add(CreateUser());
        context.Accounts.Add(new Account { Id = AccountId, Name = "A Active account" });
        context.AccountUsers.Add(new AccountUser { AccountId = AccountId, UserId = TargetUserId });
        await context.SaveChangesAsync();

        seedRequestUser.AccountId = OtherAccountId;
        context.Accounts.Add(new Account { Id = OtherAccountId, Name = "Z Revoked account" });
        context.AccountUsers.Add(new AccountUser
        {
            AccountId = OtherAccountId,
            UserId = TargetUserId,
            AccessRevokedOn = revokedOn
        });
        await context.SaveChangesAsync();

        var requestUser = new RequestUser(context);
        await requestUser.Initialize(null, TargetUserId, throwOnInvalidAccountId: false);
        context.RequestUser = requestUser;

        var result = await CreateAppService(context, requestUser).Initialize();

        Assert.Equal(AccountId, result.CurrentUser?.CurrentAccountId);
        Assert.Equal(AccountId, Assert.Single(result.AccountIds).Value);
        Assert.Equal(OtherAccountId, Assert.Single(result.RevokedAccountIds).Value);

        var selectableAccounts = (await new AuthenticationRepository(context, requestUser)
            .GetAccountIdsByUserId(TargetUserId)).ToList();
        Assert.Equal([AccountId], selectableAccounts);
    }

    [Fact]
    public async Task AllRevokedAccountsRemainVisibleWithoutSelectingOne()
    {
        await using var context = CreateContext();
        SetRequestUser(context, TargetUserId, AccountId);
        var revokedOn = DateTime.UtcNow;

        context.Users.Add(CreateUser());
        context.Accounts.Add(new Account { Id = AccountId, Name = "Revoked account" });
        context.AccountUsers.Add(new AccountUser
        {
            AccountId = AccountId,
            UserId = TargetUserId,
            InvitationAcceptedOn = DateTime.UtcNow,
            AccessRevokedOn = revokedOn
        });
        await context.SaveChangesAsync();

        var requestUser = new RequestUser(context);
        await requestUser.Initialize(null, TargetUserId, throwOnInvalidAccountId: false);
        context.RequestUser = requestUser;

        var result = await CreateAppService(context, requestUser).Initialize();

        Assert.Null(result.CurrentUser?.CurrentAccountId);
        Assert.Empty(result.AccountIds);
        Assert.Equal(AccountId, Assert.Single(result.RevokedAccountIds).Value);
    }

    [Fact]
    public async Task UserManagerProjectionIncludesRevocationState()
    {
        await using var context = CreateContext();
        var requestUser = SetRequestUser(context, TargetUserId, AccountId);
        var revokedOn = DateTime.UtcNow;
        await SeedUserAndMembership(context, requestUser, revokedOn);

        var repository = new UserRepository(context, requestUser);

        var user = Assert.Single(await repository.GetAccountUsers(AccountId));

        Assert.Equal(TargetUserId, user.UserId);
        Assert.Equal(revokedOn, user.AccessRevokedOn);
    }

    [Fact]
    public async Task UserCannotRevokeTheirOwnAccess()
    {
        await using var context = CreateContext();
        var requestUser = SetRequestUser(context, TargetUserId, AccountId);
        await SeedUserAndMembership(context, requestUser);
        var service = CreateService(context, requestUser);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.RevokeAccess(TargetUserId));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Null((await context.AccountUsers.SingleAsync()).AccessRevokedOn);
    }

    [Fact]
    public async Task RevokeAndRestorePreserveMembershipAndPermissions()
    {
        await using var context = CreateContext();
        var requestUser = SetRequestUser(context, TargetUserId, AccountId);
        await SeedUserAndMembership(context, requestUser);

        // An accepted membership must never fall back to destructive invitation removal,
        // even if legacy data happens to retain an invitation code.
        var membership = await context.AccountUsers.SingleAsync();
        membership.InvitationCode = "legacy-code";

        var cavePermission = new CavePermission
        {
            UserId = TargetUserId,
            AccountId = AccountId,
            PermissionId = "viewperm01"
        };
        var userPermission = new UserPermission
        {
            UserId = TargetUserId,
            AccountId = AccountId,
            PermissionId = "adminperm1"
        };
        context.CavePermissions.Add(cavePermission);
        context.UserPermissions.Add(userPermission);
        await context.SaveChangesAsync();
        var cavePermissionId = cavePermission.Id;
        var userPermissionId = userPermission.Id;

        requestUser.Id = AdminUserId;
        var service = CreateService(context, requestUser);

        await service.RevokeAccess(TargetUserId);

        context.ChangeTracker.Clear();
        membership = await context.AccountUsers.SingleAsync();
        var revokedOn = Assert.IsType<DateTime>(membership.AccessRevokedOn);
        var persistedCavePermission = Assert.Single(await context.CavePermissions.AsNoTracking().ToListAsync());
        Assert.Equal(cavePermissionId, persistedCavePermission.Id);
        Assert.Equal(TargetUserId, persistedCavePermission.UserId);
        Assert.Equal(AccountId, persistedCavePermission.AccountId);
        Assert.Equal("viewperm01", persistedCavePermission.PermissionId);
        var persistedUserPermission = Assert.Single(await context.UserPermissions.AsNoTracking().ToListAsync());
        Assert.Equal(userPermissionId, persistedUserPermission.Id);
        Assert.Equal(TargetUserId, persistedUserPermission.UserId);
        Assert.Equal(AccountId, persistedUserPermission.AccountId);
        Assert.Equal("adminperm1", persistedUserPermission.PermissionId);

        await service.RevokeAccess(TargetUserId);

        context.ChangeTracker.Clear();
        membership = await context.AccountUsers.SingleAsync();
        Assert.Equal(revokedOn, membership.AccessRevokedOn);

        await service.RestoreAccess(TargetUserId);

        context.ChangeTracker.Clear();
        membership = await context.AccountUsers.SingleAsync();
        Assert.Null(membership.AccessRevokedOn);
        Assert.Equal(cavePermissionId, (await context.CavePermissions.AsNoTracking().SingleAsync()).Id);
        Assert.Equal(userPermissionId, (await context.UserPermissions.AsNoTracking().SingleAsync()).Id);

        var restoredRequestUser = new RequestUser(context);
        await restoredRequestUser.Initialize(AccountId, TargetUserId);

        Assert.Equal(AccountId, restoredRequestUser.AccountId);
    }

    private static AppService CreateAppService(
        PlanarianDbContext context,
        RequestUser requestUser)
    {
        return new AppService(
            new AppRepository(context, requestUser),
            requestUser,
            new StubApiRequestOrigin(),
            new ServerOptions(),
            new UserRepository(context, requestUser));
    }

    private static AccountUserManagerService CreateService(
        PlanarianDbContext context,
        RequestUser requestUser)
    {
        var userRepository = new UserRepository(context, requestUser);
        return new AccountUserManagerService(
            userRepository,
            requestUser,
            null!,
            null!,
            new AccountRepository(context, requestUser),
            null!,
            userRepository,
            NullLogger<AccountUserManagerService>.Instance);
    }

    private static PlanarianDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestPlanarianDbContext(options);
    }

    private static RequestUser SetRequestUser(PlanarianDbContext context, string userId, string accountId)
    {
        var requestUser = new RequestUser(context)
        {
            Id = userId,
            AccountId = accountId,
            FirstName = "Test",
            LastName = "User"
        };
        context.RequestUser = requestUser;
        return requestUser;
    }

    private static async Task SeedUserAndMembership(
        PlanarianDbContext context,
        RequestUser requestUser,
        DateTime? accessRevokedOn = null)
    {
        context.Users.Add(CreateUser());
        context.AccountUsers.Add(new AccountUser
        {
            AccountId = AccountId,
            UserId = TargetUserId,
            InvitationAcceptedOn = DateTime.UtcNow,
            AccessRevokedOn = accessRevokedOn
        });

        await context.SaveChangesAsync();
        requestUser.AccountId = AccountId;
    }

    private static User CreateUser()
    {
        return new User("Target", "User", "target@example.com")
        {
            Id = TargetUserId,
            LastActiveOn = DateTime.UtcNow
        };
    }

    private sealed class StubApiRequestOrigin : IApiRequestOrigin
    {
        public string GetOrigin() => "https://api.example.test";
    }

    private sealed class TestPlanarianDbContext : PlanarianDbContext
    {
        public TestPlanarianDbContext(DbContextOptions<PlanarianDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Cave>().Ignore(e => e.NarrativeSearchVector);
        }
    }
}
