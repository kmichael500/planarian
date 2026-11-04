using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Planarian.Library.Exceptions;
using Planarian.Library.Options;
using Planarian.Modules.App.Repositories;
using Planarian.Modules.App.Services;
using Planarian.Shared.Services;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Account.Services;
using Planarian.Modules.Users.Repositories;
using Planarian.Tests.EmailDelivery.TestInfrastructure;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Users;

public sealed class AccountAccessPostgresTests
{
    private const string AccountId = "access0001";
    private const string FirstAdminId = "admin00001";
    private const string SecondAdminId = "admin00002";
    private const string InvitedUserId = "invite0001";
    private const string AdminPermissionId = "permadmin1";

    [Fact]
    public async Task ConcurrentAdministratorRevocationsAlwaysLeaveAnActiveAdministrator()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);

            // Force the two serializable transactions to reach the write boundary together.
            var beforeWrite = new ConcurrentRevocationBarrier();

            async Task<bool> Revoke(string actorId, string targetId)
            {
                await using var context = IsolatedPostgresDatabase.NewContext(connectionString, beforeWrite);
                var service = CreateService(context, actorId);
                try
                {
                    await service.RevokeAccess(targetId);
                    return true;
                }
                catch (ApiException error) when (error.StatusCode == 409)
                {
                    return false;
                }
            }

            var results = await Task.WhenAll(
                Revoke(FirstAdminId, SecondAdminId),
                Revoke(SecondAdminId, FirstAdminId));

            Assert.Equal(2, beforeWrite.ArrivalCount);
            Assert.Single(results.Where(success => success));
            Assert.Single(results.Where(success => !success));

            await using var verified = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.Equal(1, await verified.AccountUsers.CountAsync(m =>
                m.AccountId == AccountId && m.AccessRevokedOn == null));
        });
    }

    [Fact]
    public async Task RevokedMembershipIsDeniedByPostgresAndCanBeRestored()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);

            await using (var revoke = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                await CreateService(revoke, FirstAdminId).RevokeAccess(SecondAdminId);
            }

            await using (var persisted = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                Assert.Equal(2, await persisted.UserPermissions.CountAsync(p =>
                    p.AccountId == AccountId && p.PermissionId == AdminPermissionId));
                Assert.True(await persisted.Users.AnyAsync(u => u.Id == SecondAdminId));
            }

            await using (var denied = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var accountRequest = new RequestUser(denied);
                var error = await Assert.ThrowsAsync<ApiException>(() =>
                    accountRequest.Initialize(AccountId, SecondAdminId));
                Assert.Equal(403, error.StatusCode);

                var bootstrap = new RequestUser(denied);
                await bootstrap.Initialize(AccountId, SecondAdminId, throwOnInvalidAccountId: false);
                Assert.True(bootstrap.IsAuthenticated);
                Assert.Null(bootstrap.AccountId);
            }

            await using (var restore = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                await CreateService(restore, FirstAdminId).RestoreAccess(SecondAdminId);
            }

            await using var restored = IsolatedPostgresDatabase.NewContext(connectionString);
            var restoredRequest = new RequestUser(restored);
            await restoredRequest.Initialize(AccountId, SecondAdminId);
            Assert.Equal(AccountId, restoredRequest.AccountId);
        });
    }

    [Fact]
    public async Task RemovingAnInvitationRemovesItsOrphanedIdentityButRetainsDeliveryHistory()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);

            await using (var seed = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                seed.RequestUser = new RequestUser(seed) { Id = FirstAdminId, AccountId = AccountId };
                var invited = new User("Temporary", "Guest", "guest@example.test")
                {
                    Id = InvitedUserId, IsTemporary = true
                };
                var membership = new AccountUser
                {
                    AccountId = AccountId,
                    User = invited,
                    InvitationCode = "testinvite"
                };
                seed.Users.Add(invited);
                seed.AccountUsers.Add(membership);
                seed.MessageLogs.Add(new MessageLog(
                    "GenericEmail", "Email", MessagePurpose.AccountInvitation,
                    MessageProvider.Mailgun, "example.test", "invitecorr",
                    "Invitation", "guest@example.test", "Temporary Guest",
                    "Planarian", "planarian@example.test", "{}", membership));
                await seed.SaveChangesAsync();
            }

            await using (var action = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                await CreateService(action, FirstAdminId).RevokeAccess(InvitedUserId);
            }

            await using var result = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.False(await result.AccountUsers.AnyAsync(m => m.UserId == InvitedUserId));
            Assert.False(await result.Users.AnyAsync(u => u.Id == InvitedUserId));
            var log = Assert.Single(await result.MessageLogs.AsNoTracking().ToListAsync());
            Assert.Equal("invitecorr", log.ProviderCorrelationId);
            Assert.Null(log.AccountInvitationAccountId);
            Assert.Null(log.AccountInvitationUserId);
        });
    }

    [Fact]
    public async Task DowngradeRefusesToDiscardPersistedRevocations()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);

            await using (var revoke = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                await CreateService(revoke, FirstAdminId).RevokeAccess(SecondAdminId);
            }

            await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
            var target = Assert.Single(context.Database.GetMigrations()
                .Where(migration => migration.EndsWith("_v32", StringComparison.Ordinal)));

            var error = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.GetService<IMigrator>().MigrateAsync(target));
            Assert.Contains("Cannot roll back v33", error.ToString());

            await using var verified = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.NotNull((await verified.AccountUsers.AsNoTracking()
                .SingleAsync(m => m.UserId == SecondAdminId)).AccessRevokedOn);
        });
    }

    [Fact]
    public async Task RemovingTheLastAdministratorRoleIsRejectedAfterOtherAdminWasRevoked()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);
            await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
            var actor = new RequestUser(context);
            await actor.Initialize(AccountId, SecondAdminId);
            context.RequestUser = actor;

            await context.AccountUsers.Where(m => m.AccountId == AccountId &&
                                                 m.UserId == SecondAdminId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    membership => membership.AccessRevokedOn, DateTime.UtcNow));

            var service = new AccountAccessService(
                new UserRepository(context, actor), new AccountRepository(context, actor), actor);

            var error = await Assert.ThrowsAsync<ApiException>(() =>
                service.RemoveUserPermission(FirstAdminId, PermissionPolicyKey.Admin));

            Assert.Equal(409, error.StatusCode);
            Assert.Equal(2, await context.UserPermissions.CountAsync(m => m.AccountId == AccountId));
        });
    }

    [Fact]
    public async Task UserCannotRevokeOwnAccess()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);
            await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
            var error = await Assert.ThrowsAsync<ApiException>(() =>
                CreateService(context, FirstAdminId).RevokeAccess(FirstAdminId));
            Assert.Equal(400, error.StatusCode);
            Assert.Equal(2, await context.AccountUsers.CountAsync(m =>
                m.AccountId == AccountId && m.AccessRevokedOn == null));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializationListsRevokedAccountsWithoutSelectingThem(bool allRevoked)
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);
            const string revokedAccountId = "access0002";
            await using (var setup = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                setup.RequestUser = new RequestUser(setup)
                {
                    Id = FirstAdminId, AccountId = revokedAccountId
                };
                setup.Accounts.Add(new Account { Id = revokedAccountId, Name = "Revoked account" });
                setup.AccountUsers.Add(new AccountUser
                {
                    AccountId = revokedAccountId, UserId = FirstAdminId,
                    InvitationAcceptedOn = DateTime.UtcNow, AccessRevokedOn = DateTime.UtcNow
                });
                await setup.SaveChangesAsync();
                if (allRevoked)
                {
                    await setup.AccountUsers.Where(m =>
                        m.AccountId == AccountId && m.UserId == FirstAdminId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(
                            m => m.AccessRevokedOn, DateTime.UtcNow));
                }
            }

            await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
            var actor = new RequestUser(context);
            await actor.Initialize(null, FirstAdminId, throwOnInvalidAccountId: false);
            context.RequestUser = actor;

            var result = await new AppService(
                new AppRepository(context, actor), actor,
                new TestApiRequestOrigin(), new ServerOptions(),
                new UserRepository(context, actor)).Initialize();

            Assert.True(actor.IsAuthenticated);
            Assert.Contains(result.RevokedAccountIds, account => account.Value == revokedAccountId);
            if (allRevoked)
            {
                Assert.Null(result.CurrentUser?.CurrentAccountId);
                Assert.Empty(result.AccountIds);
                Assert.Equal(2, result.RevokedAccountIds.Count());
            }
            else
            {
                Assert.Equal(AccountId, result.CurrentUser?.CurrentAccountId);
                Assert.Equal(AccountId, result.AccountIds.Single().Value);
            }
        });
    }

    [Fact]
    public async Task UserGridMarksRevokedAndPendingInvitationStates()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);
            const string invitedId = "guest00001";
            await using (var setup = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(setup) { Id = FirstAdminId };
                setup.RequestUser = actor;
                setup.Users.Add(new User("Pending", "Guest", "pending@example.test")
                {
                    Id = invitedId, IsTemporary = true
                });
                await setup.SaveChangesAsync();
                actor.AccountId = AccountId;
                setup.AccountUsers.Add(new AccountUser
                {
                    AccountId = AccountId, UserId = invitedId,
                    InvitationCode = "pendcode1"
                });
                await setup.SaveChangesAsync();
            }

            await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
            var actorForQuery = new RequestUser(context) { Id = FirstAdminId, AccountId = AccountId };
            context.RequestUser = actorForQuery;
            var repository = new UserRepository(context, actorForQuery);

            var pending = Assert.Single((await repository.GetAccountUsers(AccountId))
                .Where(user => user.UserId == invitedId));
            Assert.True(pending.HasActiveInvitation);

            await context.AccountUsers.Where(m => m.UserId == invitedId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    m => m.AccessRevokedOn, DateTime.UtcNow));
            var revoked = Assert.Single((await repository.GetAccountUsers(AccountId))
                .Where(user => user.UserId == invitedId));
            Assert.NotNull(revoked.AccessRevokedOn);
            Assert.False(revoked.HasActiveInvitation);
        });
    }

    [Fact]
    public async Task RevokingAcceptedLegacyInvitationPreservesMemberAndPermissions()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);
            await using (var setup = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                await setup.AccountUsers.Where(m =>
                    m.AccountId == AccountId && m.UserId == SecondAdminId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        m => m.InvitationCode, "oldcode01"));
            }

            await using (var revoke = IsolatedPostgresDatabase.NewContext(connectionString))
                await CreateService(revoke, FirstAdminId).RevokeAccess(SecondAdminId);

            await using var verified = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.True(await verified.Users.AnyAsync(u => u.Id == SecondAdminId));
            Assert.NotNull((await verified.AccountUsers.SingleAsync(m =>
                m.UserId == SecondAdminId)).AccessRevokedOn);
            Assert.True(await verified.UserPermissions.AnyAsync(p =>
                p.AccountId == AccountId && p.UserId == SecondAdminId));
        });
    }

    [Fact]
    public async Task DuplicateLegacyInvitationIsNotShownToAnExistingMember()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);
            const string invitedId = "guest00001";
            await using (var setup = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(setup) { Id = FirstAdminId };
                setup.RequestUser = actor;
                setup.Users.Add(new User("Duplicate", "Invite", "admin1@example.test")
                {
                    Id = invitedId, IsTemporary = true
                });
                await setup.Users.Where(u => u.Id == FirstAdminId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        u => u.EmailConfirmedOn, DateTime.UtcNow));
                await setup.SaveChangesAsync();
                actor.AccountId = AccountId;
                setup.AccountUsers.Add(new AccountUser
                {
                    AccountId = AccountId, UserId = invitedId, InvitationCode = "old-invite"
                });
                await setup.SaveChangesAsync();
            }

            await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
            var actorForQuery = new RequestUser(context) { Id = FirstAdminId, AccountId = AccountId };
            context.RequestUser = actorForQuery;
            Assert.Empty(await new UserRepository(context, actorForQuery)
                .GetPendingInvitationsForCurrentUser());
        });
    }

    private sealed class TestApiRequestOrigin : IApiRequestOrigin
    {
        public string GetOrigin() => "https://api.example.test";
    }

    private sealed class ConcurrentRevocationBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource<bool> _bothReady =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivalCount;

        public int ArrivalCount => Volatile.Read(ref _arrivalCount);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            // Both transactions have read the administrator state before SaveChanges.
            Assert.NotNull(eventData.Context?.Database.CurrentTransaction);
            Assert.Single(eventData.Context!.ChangeTracker.Entries<AccountUser>()
                .Where(entry => entry.Entity.AccessRevokedOn.HasValue));

            if (Interlocked.Increment(ref _arrivalCount) == 2)
                _bothReady.TrySetResult(true);

            await _bothReady.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }

    private static AccountAccessService CreateService(PlanarianDbContext context, string actorId)
    {
        var requestUser = new RequestUser(context)
        {
            Id = actorId, AccountId = AccountId, FirstName = "Admin", LastName = "User"
        };
        context.RequestUser = requestUser;
        return new AccountAccessService(
            new UserRepository(context, requestUser),
            new AccountRepository(context, requestUser), requestUser);
    }

    private static async Task SeedTwoAdministrators(string connectionString)
    {
        await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
        context.RequestUser = new RequestUser(context)
        {
            Id = FirstAdminId, AccountId = AccountId, FirstName = "Admin", LastName = "One"
        };

        context.RequestUser.AccountId = null;
        context.Users.AddRange(
            new User("Admin", "One", "admin1@example.test") { Id = FirstAdminId },
            new User("Admin", "Two", "admin2@example.test") { Id = SecondAdminId });
        await context.SaveChangesAsync();

        context.RequestUser.AccountId = AccountId;
        context.Accounts.Add(new Account { Id = AccountId, Name = "Isolated access account" });
        context.AccountUsers.AddRange(
            new AccountUser { AccountId = AccountId, UserId = FirstAdminId,
                              InvitationAcceptedOn = DateTime.UtcNow },
            new AccountUser { AccountId = AccountId, UserId = SecondAdminId,
                              InvitationAcceptedOn = DateTime.UtcNow });
        context.Permissions.Add(new Permission
        {
            Id = AdminPermissionId, Key = PermissionPolicyKey.Admin,
            Name = "Admin", Description = "Isolated test administrator",
            PermissionType = PermissionType.User, IsHidden = false
        });
        context.UserPermissions.AddRange(
            new UserPermission { AccountId = AccountId, UserId = FirstAdminId,
                                 PermissionId = AdminPermissionId },
            new UserPermission { AccountId = AccountId, UserId = SecondAdminId,
                                 PermissionId = AdminPermissionId });

        await context.SaveChangesAsync();
    }
}
