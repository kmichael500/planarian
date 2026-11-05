using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Users.Repositories;
using Planarian.Modules.Users.Services;
using Planarian.Tests.EmailDelivery.TestInfrastructure;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Users;

public sealed class InvitationClaimPostgresTests
{
    private const string AccountId = "claim00001";
    private const string MemberId = "member0001";
    private const string InvitationUserId = "invite0001";
    private const string InvitationCode = "invcode001";
    private const string PermissionId = "perminv001";

    [Fact]
    public async Task AlreadyActiveMemberDiscardsOnlyRedundantInvitation()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedInvitation(connectionString, alreadyMember: true);

            await Claim(connectionString);

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            var membership = Assert.Single(await verify.AccountUsers.AsNoTracking().ToListAsync());
            Assert.Equal(MemberId, membership.UserId);
            Assert.Null(membership.AccessDisabledOn);
            Assert.NotNull(membership.InvitationAcceptedOn);
            Assert.True(await verify.Users.AnyAsync(u => u.Id == MemberId));
            Assert.False(await verify.Users.AnyAsync(u => u.Id == InvitationUserId));
            Assert.Equal(MemberId, Assert.Single(await verify.UserPermissions.ToListAsync()).UserId);
            Assert.Empty(await verify.CavePermissions.ToListAsync());
            var history = Assert.Single(await verify.MessageLogs.AsNoTracking().ToListAsync());
            Assert.Equal("claimlog01", history.ProviderCorrelationId);
            Assert.Null(history.AccountInvitationAccountId);
            Assert.Null(history.AccountInvitationUserId);
        });
    }

    [Fact]
    public async Task RedundantClaimDoesNotDeleteAnInvitationForAnotherAccount()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedInvitation(connectionString, alreadyMember: true);

            const string otherAccountId = "claim00002";
            const string otherCode = "otherinv01";
            await using (var seed = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                seed.RequestUser = new RequestUser(seed)
                {
                    Id = MemberId,
                    AccountId = otherAccountId
                };
                seed.Accounts.Add(new Account { Id = otherAccountId, Name = "Other invitation account" });
                seed.AccountUsers.Add(new AccountUser
                {
                    AccountId = otherAccountId,
                    UserId = InvitationUserId,
                    InvitationCode = otherCode
                });
                seed.UserPermissions.Add(new UserPermission
                {
                    UserId = InvitationUserId,
                    AccountId = otherAccountId,
                    PermissionId = PermissionId
                });
                await seed.SaveChangesAsync();
            }

            await Claim(connectionString);

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.True(await verify.Users.AnyAsync(u => u.Id == InvitationUserId));
            Assert.False(await verify.AccountUsers.AnyAsync(m =>
                m.AccountId == AccountId && m.UserId == InvitationUserId));
            Assert.True(await verify.AccountUsers.AnyAsync(m =>
                m.AccountId == otherAccountId &&
                m.UserId == InvitationUserId && m.InvitationCode == otherCode));
            Assert.True(await verify.UserPermissions.AnyAsync(p =>
                p.AccountId == otherAccountId && p.UserId == InvitationUserId));
            Assert.False(await verify.UserPermissions.AnyAsync(p =>
                p.AccountId == AccountId && p.UserId == InvitationUserId));
        });
    }

    [Fact]
    public async Task ClaimTransfersOnlyTheInvitedAccountsPermissions()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedInvitation(connectionString, alreadyMember: false);
            const string otherAccountId = "claim00002";

            await using (var seed = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                seed.RequestUser = new RequestUser(seed)
                {
                    Id = MemberId, AccountId = otherAccountId
                };
                seed.Accounts.Add(new Account { Id = otherAccountId, Name = "Second invitation account" });
                seed.AccountUsers.Add(new AccountUser
                {
                    AccountId = otherAccountId, UserId = InvitationUserId,
                    InvitationCode = "otherinv01"
                });
                seed.UserPermissions.Add(new UserPermission
                {
                    UserId = InvitationUserId, AccountId = otherAccountId, PermissionId = PermissionId
                });
                seed.CavePermissions.Add(new CavePermission
                {
                    UserId = InvitationUserId, AccountId = otherAccountId, PermissionId = PermissionId
                });
                await seed.SaveChangesAsync();
            }

            await Claim(connectionString, selectedAccountId: null);

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.True(await verify.AccountUsers.AnyAsync(m =>
                m.AccountId == AccountId && m.UserId == MemberId));
            Assert.True(await verify.AccountUsers.AnyAsync(m =>
                m.AccountId == otherAccountId && m.UserId == InvitationUserId &&
                m.InvitationCode == "otherinv01"));
            Assert.True(await verify.Users.AnyAsync(u => u.Id == InvitationUserId));

            Assert.True(await verify.UserPermissions.AnyAsync(p =>
                p.AccountId == AccountId && p.UserId == MemberId));
            Assert.True(await verify.CavePermissions.AnyAsync(p =>
                p.AccountId == AccountId && p.UserId == MemberId));

            Assert.True(await verify.UserPermissions.AnyAsync(p =>
                p.AccountId == otherAccountId && p.UserId == InvitationUserId));
            Assert.True(await verify.CavePermissions.AnyAsync(p =>
                p.AccountId == otherAccountId && p.UserId == InvitationUserId));
            Assert.False(await verify.UserPermissions.AnyAsync(p =>
                p.AccountId == otherAccountId && p.UserId == MemberId));
            Assert.False(await verify.CavePermissions.AnyAsync(p =>
                p.AccountId == otherAccountId && p.UserId == MemberId));
        });
    }

    [Fact]
    public async Task DisabledMemberCannotUseInvitationToRestoreAccessOrRemoveItsHistory()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedInvitation(connectionString, alreadyMember: true, disabled: true);

            var error = await Assert.ThrowsAsync<ApiException>(() =>
                Claim(connectionString, selectedAccountId: null));
            Assert.Equal(409, error.StatusCode);

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.NotNull((await verify.AccountUsers.SingleAsync(m =>
                m.UserId == MemberId)).AccessDisabledOn);
            Assert.True(await verify.AccountUsers.AnyAsync(m => m.UserId == InvitationUserId));
            Assert.True(await verify.Users.AnyAsync(u => u.Id == InvitationUserId));
            Assert.Equal(2, await verify.UserPermissions.CountAsync());
            Assert.Equal(1, await verify.CavePermissions.CountAsync());
            var history = Assert.Single(await verify.MessageLogs.AsNoTracking().ToListAsync());
            Assert.Equal(InvitationUserId, history.AccountInvitationUserId);
        });
    }

    [Fact]
    public async Task NewMemberReceivesInvitedPermissionsAndInvitationHistory()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedInvitation(connectionString, alreadyMember: false);

            // A user with no memberships cannot select the invited account yet.
            await Claim(connectionString, selectedAccountId: null);

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            var membership = Assert.Single(await verify.AccountUsers.AsNoTracking().ToListAsync());
            Assert.Equal(MemberId, membership.UserId);
            Assert.NotNull(membership.InvitationAcceptedOn);
            Assert.False(await verify.Users.AnyAsync(u => u.Id == InvitationUserId));
            Assert.Equal(MemberId, Assert.Single(await verify.UserPermissions.ToListAsync()).UserId);
            Assert.Equal(MemberId, Assert.Single(await verify.CavePermissions.ToListAsync()).UserId);
            var history = Assert.Single(await verify.MessageLogs.AsNoTracking().ToListAsync());
            Assert.Equal(AccountId, history.AccountInvitationAccountId);
            Assert.Equal(MemberId, history.AccountInvitationUserId);
        });
    }

    [Fact]
    public async Task DecliningAnInvitationRemovesItsPermissionsWithoutDeletingHistory()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedInvitation(connectionString, alreadyMember: true);

            await using (var context = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(context);
                context.RequestUser = actor;
                await new InvitationLifecycleService(new UserRepository(context, actor), actor)
                    .Decline(InvitationCode);
            }

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.Single(await verify.AccountUsers.ToListAsync());
            Assert.False(await verify.Users.AnyAsync(u => u.Id == InvitationUserId));
            Assert.Equal(MemberId, Assert.Single(await verify.UserPermissions.ToListAsync()).UserId);
            Assert.Empty(await verify.CavePermissions.ToListAsync());
            var history = Assert.Single(await verify.MessageLogs.ToListAsync());
            Assert.Equal("claimlog01", history.ProviderCorrelationId);
            Assert.Null(history.AccountInvitationUserId);
        });
    }

    [Fact]
    public async Task NonexistentCodeDoesNotDeleteExistingMembershipOrPermissions()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedInvitation(connectionString, alreadyMember: true);

            var error = await Assert.ThrowsAsync<ApiException>(() =>
                Claim(connectionString, "notfound01"));
            Assert.Equal(404, error.StatusCode);

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.Equal(2, await verify.AccountUsers.CountAsync());
            Assert.Equal(2, await verify.Users.CountAsync());
            Assert.Equal(2, await verify.UserPermissions.CountAsync());
            Assert.True(await verify.AccountUsers.AnyAsync(m =>
                m.UserId == InvitationUserId && m.InvitationCode == InvitationCode));
        });
    }

    private static async Task Claim(string connectionString, string code = InvitationCode,
        string? selectedAccountId = AccountId)
    {
        await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
        var requestUser = new RequestUser(context)
        {
            Id = MemberId,
            AccountId = selectedAccountId
        };
        context.RequestUser = requestUser;
        var repository = new UserRepository(context, requestUser);
        var member = await repository.Get(MemberId) ?? throw new InvalidOperationException("Missing test actor.");
        var service = new InvitationLifecycleService(repository, requestUser);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await service.Claim(member, code, CancellationToken.None);
        await transaction.CommitAsync();
    }

    private static async Task SeedInvitation(string connectionString, bool alreadyMember, bool disabled = false)
    {
        await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
        context.RequestUser = new RequestUser(context) { Id = MemberId };

        context.Users.AddRange(
            new User("Existing", "Member", "member@example.test")
            {
                Id = MemberId,
                EmailConfirmedOn = DateTime.UtcNow
            },
            new User("Pending", "Invitation", "member@example.test")
            {
                Id = InvitationUserId,
                IsTemporary = true
            });
        await context.SaveChangesAsync();

        context.RequestUser.AccountId = AccountId;
        context.Accounts.Add(new Account { Id = AccountId, Name = "Invitation test account" });
        if (alreadyMember)
            context.AccountUsers.Add(new AccountUser
            {
                AccountId = AccountId,
                UserId = MemberId,
                InvitationAcceptedOn = DateTime.UtcNow,
                AccessDisabledOn = disabled ? DateTime.UtcNow : null
            });

        var invitation = new AccountUser
        {
            AccountId = AccountId,
            UserId = InvitationUserId,
            InvitationCode = InvitationCode,
            InvitationSentOn = DateTime.UtcNow
        };
        context.AccountUsers.Add(invitation);
        context.Permissions.Add(new Permission
        {
            Id = PermissionId,
            Name = "Test permission",
            Description = "Test invitation permission transfer and isolation",
            Key = PermissionPolicyKey.Admin,
            PermissionType = PermissionType.User
        });
        if (alreadyMember)
            context.UserPermissions.Add(new UserPermission
            {
                UserId = MemberId, AccountId = AccountId, PermissionId = PermissionId
            });

        context.UserPermissions.Add(new UserPermission
        {
            UserId = InvitationUserId, AccountId = AccountId, PermissionId = PermissionId
        });
        context.CavePermissions.Add(new CavePermission
        {
            UserId = InvitationUserId, AccountId = AccountId, PermissionId = PermissionId
        });
        context.MessageLogs.Add(new MessageLog(
            "GenericEmail", "Email", MessagePurpose.AccountInvitation,
            MessageProvider.Mailgun, "example.test", "claimlog01",
            "Invitation", "member@example.test", "Existing Member",
            "Planarian", "planarian@example.test", "{}", invitation));
        await context.SaveChangesAsync();
    }
}
