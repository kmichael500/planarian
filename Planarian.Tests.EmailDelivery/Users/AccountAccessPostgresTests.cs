using System.Data.Common;
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
    public async Task ConcurrentAccessDisablingOperationsLeaveAnActiveAdministrator()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);

            // Force the two serializable transactions to reach the write boundary together.
            var beforeWrite = new ConcurrentDisablingBarrier();

            async Task<bool> Disable(string actorId, string targetId)
            {
                await using var context = IsolatedPostgresDatabase.NewContext(connectionString, beforeWrite);
                var service = CreateService(context, actorId);
                try
                {
                    await service.DisableAccess(targetId, disableMessage: "Access review.");
                    return true;
                }
                catch (ApiException error) when (error.StatusCode == 409)
                {
                    return false;
                }
            }

            var results = await Task.WhenAll(
                Disable(FirstAdminId, SecondAdminId),
                Disable(SecondAdminId, FirstAdminId));

            Assert.Equal(2, beforeWrite.ArrivalCount);
            Assert.Single(results.Where(success => success));
            Assert.Single(results.Where(success => !success));

            await using var verified = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.Equal(1, await verified.AccountUsers.CountAsync(m =>
                m.AccountId == AccountId && m.AccessDisabledOn == null));
        });
    }

    [Fact]
    public async Task ConcurrentMembershipRevocationsLeaveAnActiveAdministrator()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);
            var beforeDelete = new ConcurrentMembershipRevocationBarrier();

            async Task<bool> RevokeMembership(string actorId, string targetId)
            {
                await using var context = IsolatedPostgresDatabase.NewContext(connectionString, beforeDelete);
                try
                {
                    await CreateService(context, actorId).RevokeMembership(targetId);
                    return true;
                }
                catch (ApiException exception) when (exception.StatusCode == 409)
                {
                    return false;
                }
            }

            var results = await Task.WhenAll(
                RevokeMembership(FirstAdminId, SecondAdminId),
                RevokeMembership(SecondAdminId, FirstAdminId));

            Assert.Equal(2, beforeDelete.ArrivalCount);
            Assert.Single(results.Where(success => success));
            Assert.Single(results.Where(success => !success));

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.Equal(1, await verify.AccountUsers.CountAsync(m =>
                m.AccountId == AccountId && m.AccessDisabledOn == null));
            Assert.Equal(1, await verify.UserPermissions.CountAsync(permission =>
                permission.AccountId == AccountId && permission.PermissionId == AdminPermissionId));
        });
    }

    [Fact]
    public async Task DisabledMembershipIsDeniedByPostgresAndCanBeRestored()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);

            await using (var disable = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                await CreateService(disable, FirstAdminId).DisableAccess(SecondAdminId, disableMessage: "Access review.");
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t\n  ")]
    public async Task AcceptedMembershipCannotBeDisabledWithoutMessage(string? message)
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);

            await using (var action = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var error = await Assert.ThrowsAsync<ApiException>(() =>
                    CreateService(action, FirstAdminId).DisableAccess(
                        SecondAdminId, disableMessage: message));
                Assert.Equal(400, error.StatusCode);
            }

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            var membership = await verify.AccountUsers.SingleAsync(m =>
                m.AccountId == AccountId && m.UserId == SecondAdminId);
            Assert.Null(membership.AccessDisabledOn);
            Assert.Null(membership.AccessDisabledMessage);
            Assert.True(await verify.UserPermissions.AnyAsync(permission =>
                permission.AccountId == AccountId && permission.UserId == SecondAdminId));
        });
    }

    [Fact]
    public async Task DisableMessageBelongsToMembershipAndIsClearedOnRestore()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);

            await using (var action = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var service = CreateService(action, FirstAdminId);
                var tooLong = await Assert.ThrowsAsync<ApiException>(() =>
                    service.DisableAccess(SecondAdminId, disableMessage: new string('x', 1001)));
                Assert.Equal(400, tooLong.StatusCode);

                Assert.True(await service.DisableAccess(SecondAdminId,
                    disableMessage: "  Annual membership review  "));
                Assert.False(await service.DisableAccess(SecondAdminId,
                    disableMessage: "Do not replace an existing disabling"));
            }

            await using (var verify = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var membership = await verify.AccountUsers.SingleAsync(m =>
                    m.AccountId == AccountId && m.UserId == SecondAdminId);
                Assert.NotNull(membership.AccessDisabledOn);
                Assert.Equal("Annual membership review", membership.AccessDisabledMessage);
                var owner = new RequestUser(verify) { Id = SecondAdminId };
                verify.RequestUser = owner;
                var disabled = await new AppRepository(verify, owner).GetDisabledAccountIds();
                Assert.Equal("Annual membership review",
                    Assert.Single(disabled).DisableMessage);
            }

            await using (var restore = IsolatedPostgresDatabase.NewContext(connectionString))
                await CreateService(restore, FirstAdminId).RestoreAccess(SecondAdminId);

            await using var restored = IsolatedPostgresDatabase.NewContext(connectionString);
            var final = await restored.AccountUsers.SingleAsync(m =>
                m.AccountId == AccountId && m.UserId == SecondAdminId);
            Assert.Null(final.AccessDisabledOn);
            Assert.Null(final.AccessDisabledMessage);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MembershipRevocationDeletesOnlyMembershipDataAndPreservesSharedRecords(bool disableFirst)
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);
            const string otherAccountId = "access0002";
            const string stateId = "state00001";
            const string firstCountyId = "county0001";
            const string secondCountyId = "county0002";
            const string firstCaveId = "cave000001";
            const string secondCaveId = "cave000002";

            await using (var seed = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(seed) { Id = FirstAdminId };
                seed.RequestUser = actor;
                await seed.Database.ExecuteSqlInterpolatedAsync(
                    $@"INSERT INTO ""States"" (""Id"", ""Name"", ""Abbreviation"", ""CreatedOn"")
                       VALUES ({stateId}, {"Tennessee"}, {"TN"}, {DateTime.UtcNow})");

                actor.AccountId = AccountId;
                seed.Counties.Add(new County
                {
                    Id = firstCountyId, AccountId = AccountId, StateId = stateId,
                    DisplayId = "001", Name = "First County"
                });
                seed.Caves.Add(new Cave
                {
                    Id = firstCaveId, AccountId = AccountId, StateId = stateId,
                    CountyId = firstCountyId, CountyNumber = 1, Name = "Original cave",
                    ReportedByUserId = SecondAdminId
                });
                seed.Favorites.AddRange(
                    new Favorite { AccountId = AccountId, UserId = SecondAdminId, CaveId = firstCaveId },
                    new Favorite { AccountId = AccountId, UserId = FirstAdminId, CaveId = firstCaveId });
                seed.CavePermissions.Add(new CavePermission
                {
                    AccountId = AccountId, UserId = SecondAdminId,
                    CaveId = firstCaveId, PermissionId = AdminPermissionId
                });
                var invitation = await seed.AccountUsers.SingleAsync(m =>
                    m.AccountId == AccountId && m.UserId == SecondAdminId);
                seed.MessageLogs.Add(new MessageLog(
                    "GenericEmail", "Email", MessagePurpose.AccountInvitation,
                    MessageProvider.Mailgun, "example.test", "permcorr01",
                    "Invitation", "admin2@example.test", "Admin Two",
                    "Planarian", "planarian@example.test", "{}", invitation));
                await seed.SaveChangesAsync();

                actor.AccountId = otherAccountId;
                seed.Accounts.Add(new Account { Id = otherAccountId, Name = "Second account" });
                seed.Counties.Add(new County
                {
                    Id = secondCountyId, AccountId = otherAccountId, StateId = stateId,
                    DisplayId = "002", Name = "Second County"
                });
                seed.Caves.Add(new Cave
                {
                    Id = secondCaveId, AccountId = otherAccountId, StateId = stateId,
                    CountyId = secondCountyId, CountyNumber = 1, Name = "Other cave"
                });
                seed.AccountUsers.Add(new AccountUser
                {
                    AccountId = otherAccountId, UserId = SecondAdminId,
                    InvitationAcceptedOn = DateTime.UtcNow
                });
                seed.UserPermissions.Add(new UserPermission
                {
                    AccountId = otherAccountId, UserId = SecondAdminId,
                    PermissionId = AdminPermissionId
                });
                seed.CavePermissions.Add(new CavePermission
                {
                    AccountId = otherAccountId, UserId = SecondAdminId,
                    CaveId = secondCaveId, PermissionId = AdminPermissionId
                });
                seed.Favorites.Add(new Favorite
                {
                    AccountId = otherAccountId, UserId = SecondAdminId, CaveId = secondCaveId
                });
                await seed.SaveChangesAsync();
            }

            await using (var action = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var service = CreateService(action, FirstAdminId);
                if (disableFirst)
                    await service.DisableAccess(SecondAdminId, disableMessage: "Access review.");

                await service.RevokeMembership(SecondAdminId);

                var missing = await Assert.ThrowsAsync<ApiException>(() =>
                    service.RevokeMembership(SecondAdminId));
                Assert.Equal(404, missing.StatusCode);
                await service.EnsureCanInvite("admin2@example.test");
            }

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.False(await verify.AccountUsers.AnyAsync(m =>
                m.AccountId == AccountId && m.UserId == SecondAdminId));
            Assert.False(await verify.UserPermissions.AnyAsync(p =>
                p.AccountId == AccountId && p.UserId == SecondAdminId));
            Assert.False(await verify.CavePermissions.AnyAsync(p =>
                p.AccountId == AccountId && p.UserId == SecondAdminId));
            Assert.False(await verify.Favorites.AnyAsync(f =>
                f.AccountId == AccountId && f.UserId == SecondAdminId));

            Assert.True(await verify.Users.AnyAsync(user => user.Id == SecondAdminId));
            Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave =>
                cave.AccountId == AccountId && cave.Id == firstCaveId &&
                cave.ReportedByUserId == SecondAdminId));
            Assert.True(await verify.Favorites.AnyAsync(f =>
                f.AccountId == AccountId && f.UserId == FirstAdminId));
            Assert.True(await verify.AccountUsers.AnyAsync(m =>
                m.AccountId == AccountId && m.UserId == FirstAdminId));
            Assert.True(await verify.AccountUsers.AnyAsync(m =>
                m.AccountId == otherAccountId && m.UserId == SecondAdminId));
            Assert.True(await verify.UserPermissions.AnyAsync(p =>
                p.AccountId == otherAccountId && p.UserId == SecondAdminId));
            Assert.True(await verify.CavePermissions.AnyAsync(p =>
                p.AccountId == otherAccountId && p.UserId == SecondAdminId));
            Assert.True(await verify.Favorites.AnyAsync(f =>
                f.AccountId == otherAccountId && f.UserId == SecondAdminId));

            var log = await verify.MessageLogs.SingleAsync(m =>
                m.ProviderCorrelationId == "permcorr01");
            Assert.Null(log.AccountInvitationAccountId);
            Assert.Null(log.AccountInvitationUserId);

            var denied = new RequestUser(verify);
            await Assert.ThrowsAsync<ApiException>(() =>
                denied.Initialize(AccountId, SecondAdminId));
            var allowed = new RequestUser(verify);
            await allowed.Initialize(otherAccountId, SecondAdminId);
            Assert.Equal(otherAccountId, allowed.AccountId);
        });
    }

    [Fact]
    public async Task MembershipRevocationRejectsSelfAndDoesNotChangeMemberships()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);
            await using var action = IsolatedPostgresDatabase.NewContext(connectionString);
            var service = CreateService(action, FirstAdminId);
            var error = await Assert.ThrowsAsync<ApiException>(() =>
                service.RevokeMembership(FirstAdminId));
            Assert.Equal(400, error.StatusCode);

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.Equal(2, await verify.AccountUsers.CountAsync(m => m.AccountId == AccountId));
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
                await CreateService(action, FirstAdminId).DisableAccess(InvitedUserId);
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
    public async Task DowngradeRefusesToDiscardPersistedDisabledAccess()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);

            await using (var disable = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                await CreateService(disable, FirstAdminId).DisableAccess(SecondAdminId, disableMessage: "Access review.");
            }

            await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
            var target = Assert.Single(context.Database.GetMigrations()
                .Where(migration => migration.EndsWith("_v32", StringComparison.Ordinal)));

            var error = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.GetService<IMigrator>().MigrateAsync(target));
            Assert.Contains("Cannot roll back v33", error.ToString());

            await using var verified = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.NotNull((await verified.AccountUsers.AsNoTracking()
                .SingleAsync(m => m.UserId == SecondAdminId)).AccessDisabledOn);
        });
    }

    [Fact]
    public async Task RemovingTheLastAdministratorRoleIsRejectedAfterOtherAdminWasDisabled()
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
                    membership => membership.AccessDisabledOn, DateTime.UtcNow));

            var service = new AccountAccessService(
                new UserRepository(context, actor), new AccountRepository(context, actor), actor);

            var error = await Assert.ThrowsAsync<ApiException>(() =>
                service.RemoveUserPermission(FirstAdminId, PermissionPolicyKey.Admin));

            Assert.Equal(409, error.StatusCode);
            Assert.Equal(2, await context.UserPermissions.CountAsync(m => m.AccountId == AccountId));
        });
    }

    [Fact]
    public async Task UserCannotDisableOwnAccess()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);
            await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
            var error = await Assert.ThrowsAsync<ApiException>(() =>
                CreateService(context, FirstAdminId).DisableAccess(FirstAdminId));
            Assert.Equal(400, error.StatusCode);
            Assert.Equal(2, await context.AccountUsers.CountAsync(m =>
                m.AccountId == AccountId && m.AccessDisabledOn == null));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializationListsDisabledAccountsWithoutSelectingThem(bool allDisabled)
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedTwoAdministrators(connectionString);
            const string disabledAccountId = "access0002";
            await using (var setup = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                setup.RequestUser = new RequestUser(setup)
                {
                    Id = FirstAdminId, AccountId = disabledAccountId
                };
                setup.Accounts.Add(new Account { Id = disabledAccountId, Name = "Disabled account" });
                setup.AccountUsers.Add(new AccountUser
                {
                    AccountId = disabledAccountId, UserId = FirstAdminId,
                    InvitationAcceptedOn = DateTime.UtcNow, AccessDisabledOn = DateTime.UtcNow
                });
                await setup.SaveChangesAsync();
                if (allDisabled)
                {
                    await setup.AccountUsers.Where(m =>
                        m.AccountId == AccountId && m.UserId == FirstAdminId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(
                            m => m.AccessDisabledOn, DateTime.UtcNow));
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
            Assert.Contains(result.DisabledAccountIds, account => account.Value == disabledAccountId);
            if (allDisabled)
            {
                Assert.Null(result.CurrentUser?.CurrentAccountId);
                Assert.Empty(result.AccountIds);
                Assert.Equal(2, result.DisabledAccountIds.Count());
            }
            else
            {
                Assert.Equal(AccountId, result.CurrentUser?.CurrentAccountId);
                Assert.Equal(AccountId, result.AccountIds.Single().Value);
            }
        });
    }

    [Fact]
    public async Task UserGridMarksDisabledAndPendingInvitationStates()
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
                    m => m.AccessDisabledOn, DateTime.UtcNow));
            var disabled = Assert.Single((await repository.GetAccountUsers(AccountId))
                .Where(user => user.UserId == invitedId));
            Assert.NotNull(disabled.AccessDisabledOn);
            Assert.False(disabled.HasActiveInvitation);
        });
    }

    [Fact]
    public async Task DisablingAcceptedLegacyInvitationPreservesMemberAndPermissions()
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

            await using (var disable = IsolatedPostgresDatabase.NewContext(connectionString))
                await CreateService(disable, FirstAdminId).DisableAccess(SecondAdminId, disableMessage: "Access review.");

            await using var verified = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.True(await verified.Users.AnyAsync(u => u.Id == SecondAdminId));
            Assert.NotNull((await verified.AccountUsers.SingleAsync(m =>
                m.UserId == SecondAdminId)).AccessDisabledOn);
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

    private sealed class ConcurrentMembershipRevocationBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> _bothReady =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivalCount;

        public int ArrivalCount => Volatile.Read(ref _arrivalCount);

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("DELETE", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("\"Favorites\"", StringComparison.Ordinal))
            {
                Assert.NotNull(eventData.Context?.Database.CurrentTransaction);
                if (Interlocked.Increment(ref _arrivalCount) == 2)
                    _bothReady.TrySetResult(true);

                await _bothReady.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }

            return result;
        }
    }

    private sealed class ConcurrentDisablingBarrier : SaveChangesInterceptor
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
                .Where(entry => entry.Entity.AccessDisabledOn.HasValue));

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
