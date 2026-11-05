using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Library.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Southport.Messaging.Email.Core;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Account.Services;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Email.Services;
using Planarian.Shared.Options;
using Planarian.Shared.Services;
using Planarian.Tests.EmailDelivery.TestInfrastructure;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Users;

public sealed class BulkInvitationPostgresTests
{
    private const string AccountId = "bulkacc001";
    private const string OtherAccountId = "bulkacc002";
    private const string AdministratorId = "admin00001";

    [Fact]
    public async Task RepeatedInvitationReturnsExistingPendingIdentityWithoutAnotherUser()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedAccounts(connectionString);
            var first = await Invite(connectionString, AccountId, " Jane@Example.Test ");
            var second = await Invite(connectionString, AccountId, "jane@example.test");

            Assert.False(first.AlreadyInvited);
            Assert.True(second.AlreadyInvited);
            Assert.Equal(first.UserId, second.UserId);

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.Single(await verify.Users.Where(u => u.IsTemporary).ToListAsync());
            Assert.Single(await verify.AccountUsers.Where(m =>
                m.AccountId == AccountId && m.InvitationCode != null).ToListAsync());
        });
    }

    [Fact]
    public async Task ConcurrentInvitationsForSameEmailCreateOnlyOnePendingInvitation()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedAccounts(connectionString);
            var results = await Task.WhenAll(
                Enumerable.Range(0, 5).Select(index => Invite(
                    connectionString, AccountId,
                    index % 2 == 0 ? "JANE@example.test" : "jane@example.test")));

            Assert.Equal(1, results.Count(result => !result.AlreadyInvited));
            Assert.Equal(4, results.Count(result => result.AlreadyInvited));
            Assert.Single(results.Select(result => result.UserId).Distinct());

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.Single(await verify.Users.Where(u => u.IsTemporary).ToListAsync());
            Assert.Single(await verify.AccountUsers.Where(m =>
                m.AccountId == AccountId && m.InvitationCode != null).ToListAsync());
        });
    }

    [Fact]
    public async Task InvitationsToDifferentAccountsRemainIndependent()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedAccounts(connectionString);
            var first = await Invite(connectionString, AccountId, "jane@example.test");
            var second = await Invite(connectionString, OtherAccountId, "jane@example.test");

            Assert.False(first.AlreadyInvited);
            Assert.False(second.AlreadyInvited);
            Assert.NotEqual(first.UserId, second.UserId);

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.Equal(2, await verify.Users.CountAsync(u => u.IsTemporary));
            Assert.Equal(1, await verify.AccountUsers.CountAsync(m =>
                m.AccountId == AccountId && m.UserId == first.UserId));
            Assert.Equal(1, await verify.AccountUsers.CountAsync(m =>
                m.AccountId == OtherAccountId && m.UserId == second.UserId));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingMembershipIsNotReinvitedOrReactivated(bool disabled)
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedAccounts(connectionString);
            const string memberId = "member0001";
            await using (var seed = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                seed.RequestUser = new RequestUser(seed)
                    { Id = memberId, AccountId = AccountId };
                seed.Users.Add(new User("Other", "Member", "member@example.test") { Id = memberId });
                seed.AccountUsers.Add(new AccountUser
                {
                    AccountId = AccountId,
                    UserId = memberId,
                    InvitationAcceptedOn = DateTime.UtcNow,
                    AccessDisabledOn = disabled ? DateTime.UtcNow : null
                });
                await seed.SaveChangesAsync();
            }

            var error = await Assert.ThrowsAsync<ApiException>(() =>
                Invite(connectionString, AccountId, "member@example.test"));
            Assert.Equal(disabled ? 409 : 400, error.StatusCode);

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.False(await verify.Users.AnyAsync(u => u.IsTemporary));
            Assert.Single(await verify.AccountUsers.Where(m =>
                m.AccountId == AccountId && m.UserId == memberId &&
                m.AccessDisabledOn.HasValue == disabled).ToListAsync());
        });
    }

    [Fact]
    public async Task DisablingEmailFailureDoesNotRollbackAccessAndPendingInvitesAreNotEmailed()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedAccounts(connectionString);
            const string memberId = "member0001";
            const string pendingId = "pending001";
            const string explanation = " \nAnnual membership review\n\n  Please contact us.  \n ";
            const string normalizedExplanation = "Annual membership review\n\n  Please contact us.";
            await using (var seed = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(seed) { Id = AdministratorId };
                seed.RequestUser = actor;
                seed.Users.AddRange(
                    new User("Member", "Example", "member@example.test") { Id = memberId },
                    new User("Pending", "Example", "pending@example.test")
                        { Id = pendingId, IsTemporary = true });
                await seed.SaveChangesAsync();
                actor.AccountId = AccountId;
                seed.AccountUsers.AddRange(
                    new AccountUser
                    {
                        AccountId = AccountId, UserId = memberId,
                        InvitationAcceptedOn = DateTime.UtcNow
                    },
                    new AccountUser
                    {
                        AccountId = AccountId, UserId = pendingId,
                        InvitationCode = "pendcode1"
                    });
                await seed.SaveChangesAsync();
            }

            await using (var action = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(action) { Id = AdministratorId, AccountId = AccountId };
                action.RequestUser = actor;
                var manager = CreateManager(action, actor);
                var missingMessage = await Assert.ThrowsAsync<ApiException>(() =>
                    manager.DisableAccess(memberId));
                Assert.Equal(400, missingMessage.StatusCode);

                var disabled = await manager.DisableAccess(memberId, explanation);
                Assert.Equal(MessageDeliveryStatus.SendFailed, disabled.NotificationEmailDeliveryStatus);
                var canceled = await manager.DisableAccess(pendingId, "Never email a pending invitee");
                Assert.Null(canceled.NotificationEmailDeliveryStatus);
                var repeated = await manager.DisableAccess(memberId, "Do not resend email");
                Assert.Null(repeated.NotificationEmailDeliveryStatus);
            }

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            var membership = await verify.AccountUsers.SingleAsync(m =>
                m.AccountId == AccountId && m.UserId == memberId);
            Assert.NotNull(membership.AccessDisabledOn);
            Assert.Equal(normalizedExplanation, membership.AccessDisabledMessage);
            Assert.False(await verify.AccountUsers.AnyAsync(m => m.UserId == pendingId));
            var email = Assert.Single(await verify.MessageLogs.Where(m =>
                m.Purpose == MessagePurpose.AccountAccessDisabled).ToListAsync());
            Assert.Equal("member@example.test", email.ToEmailAddress);
            Assert.Equal(MessageDeliveryStatus.SendFailed, email.DeliveryStatus);
            Assert.Equal("Planarian", email.FromName);
            Assert.Equal("notifications@example.test", email.FromEmailAddress);
            using var substitutions = System.Text.Json.JsonDocument.Parse(email.Substitutions);
            var paragraphs = substitutions.RootElement.GetProperty("messages")
                .EnumerateArray().Select(element => element.GetString()).ToList();
            Assert.Equal(new[] { "Annual membership review", "", "  Please contact us." }, paragraphs);
            Assert.DoesNotContain("by an account administrator", email.Substitutions);
            Assert.Contains(AccountId, email.Subject);
        });
    }

    [Fact]
    public async Task DisablingAccessWithoutEmailStoresNoMessageAndCreatesNoEmail()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedAccounts(connectionString);
            const string memberId = "member0001";
            await using (var seed = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(seed) { Id = AdministratorId };
                seed.RequestUser = actor;
                seed.Users.Add(new User("Member", "Example", "member@example.test") { Id = memberId });
                await seed.SaveChangesAsync();
                actor.AccountId = AccountId;
                seed.AccountUsers.Add(new AccountUser
                {
                    AccountId = AccountId, UserId = memberId, InvitationAcceptedOn = DateTime.UtcNow
                });
                await seed.SaveChangesAsync();
            }

            await using (var action = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(action) { Id = AdministratorId, AccountId = AccountId };
                action.RequestUser = actor;
                var result = await CreateManager(action, actor).DisableAccess(
                    memberId, sendEmail: false);
                Assert.Null(result.NotificationEmailDeliveryStatus);
            }

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            var membership = await verify.AccountUsers.SingleAsync(m =>
                m.AccountId == AccountId && m.UserId == memberId);
            Assert.NotNull(membership.AccessDisabledOn);
            Assert.Null(membership.AccessDisabledMessage);
            Assert.Empty(await verify.MessageLogs.ToListAsync());
        });
    }

    [Fact]
    public async Task MembershipRevocationEmailFailureDoesNotRestoreMembership()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedAccounts(connectionString);
            const string memberId = "member0001";
            const string explanation = "Membership has ended.";
            await using (var seed = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(seed) { Id = AdministratorId };
                seed.RequestUser = actor;
                seed.Users.Add(new User("Member", "Example", "member@example.test") { Id = memberId });
                await seed.SaveChangesAsync();
                actor.AccountId = AccountId;
                seed.AccountUsers.Add(new AccountUser
                {
                    AccountId = AccountId, UserId = memberId, InvitationAcceptedOn = DateTime.UtcNow
                });
                await seed.SaveChangesAsync();
            }

            await using (var action = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(action) { Id = AdministratorId, AccountId = AccountId };
                action.RequestUser = actor;
                var manager = CreateManager(action, actor);
                var missing = await Assert.ThrowsAsync<ApiException>(() =>
                    manager.RevokeMembership(memberId, sendEmail: true));
                Assert.Equal(400, missing.StatusCode);
                var tooLong = await Assert.ThrowsAsync<ApiException>(() =>
                    manager.RevokeMembership(memberId, notificationMessage: new string('x', 1001),
                        sendEmail: true));
                Assert.Equal(400, tooLong.StatusCode);

                var result = await manager.RevokeMembership(memberId,
                    notificationMessage: "  " + explanation + "  ", sendEmail: true);
                Assert.Equal(MessageDeliveryStatus.SendFailed, result.NotificationEmailDeliveryStatus);
            }

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.False(await verify.AccountUsers.AnyAsync(m =>
                m.AccountId == AccountId && m.UserId == memberId));
            Assert.True(await verify.Users.AnyAsync(u => u.Id == memberId));
            var email = Assert.Single(await verify.MessageLogs.ToListAsync());
            Assert.Equal(MessagePurpose.AccountMembershipRevoked, email.Purpose);
            Assert.Equal("GenericEmail", email.TemplateKey);
            Assert.Equal(MessageDeliveryStatus.SendFailed, email.DeliveryStatus);
            Assert.Contains("has been revoked", email.Subject);
            using var substitutions = System.Text.Json.JsonDocument.Parse(email.Substitutions);
            Assert.Equal("Membership revoked",
                substitutions.RootElement.GetProperty("header").GetString());
            Assert.Equal(explanation,
                Assert.Single(substitutions.RootElement.GetProperty("messages").EnumerateArray())
                    .GetString());
        });
    }

    [Fact]
    public async Task MembershipRevocationCanSkipEmailAndMessage()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedAccounts(connectionString);
            const string memberId = "member0001";
            await using (var seed = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(seed) { Id = AdministratorId };
                seed.RequestUser = actor;
                seed.Users.Add(new User("Member", "Example", "member@example.test") { Id = memberId });
                await seed.SaveChangesAsync();
                actor.AccountId = AccountId;
                seed.AccountUsers.Add(new AccountUser
                {
                    AccountId = AccountId, UserId = memberId, InvitationAcceptedOn = DateTime.UtcNow
                });
                await seed.SaveChangesAsync();
            }

            await using (var action = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(action) { Id = AdministratorId, AccountId = AccountId };
                action.RequestUser = actor;
                var result = await CreateManager(action, actor).RevokeMembership(
                    memberId, sendEmail: false);
                Assert.Null(result.NotificationEmailDeliveryStatus);
            }

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.False(await verify.AccountUsers.AnyAsync(m =>
                m.AccountId == AccountId && m.UserId == memberId));
            Assert.Empty(await verify.MessageLogs.ToListAsync());
        });
    }

    [Theory]
    [InlineData(null, "notifications@example.test")]
    [InlineData("planarian.xyz", "notifications@planarian.xyz")]
    public async Task PasswordChangeEmailUsesFromDomainOverrideAndConfiguredSupport(string? fromDomain, string expectedFromEmail)
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedAccounts(connectionString);
            await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
            var actor = new RequestUser(context) { Id = AdministratorId, AccountId = AccountId };
            context.RequestUser = actor;

            await CreateEmailService(context, actor, fromDomain).SendPasswordChangedEmail(
                "member@example.test", "Example Member");

            var log = Assert.Single(await context.MessageLogs.AsNoTracking().ToListAsync());
            Assert.Equal(MessagePurpose.PasswordChanged, log.Purpose);
            Assert.Equal("Planarian", log.FromName);
            Assert.Equal(expectedFromEmail, log.FromEmailAddress);
            using var substitutions = System.Text.Json.JsonDocument.Parse(log.Substitutions);
            var message = Assert.Single(substitutions.RootElement.GetProperty("messages")
                .EnumerateArray()).GetString();
            Assert.Equal("Your password was changed. If you did not make this change, please contact Example Support & Assistance immediately at help@example.org.", message);
            Assert.False(substitutions.RootElement.TryGetProperty("supportEmail", out _));
            Assert.False(substitutions.RootElement.TryGetProperty("supportName", out _));
        });
    }

    private static AccountUserManagerService CreateManager(PlanarianDbContext context, RequestUser actor)
    {
        var users = new UserRepository(context, actor);
        var accounts = new AccountRepository(context, actor);
        return new AccountUserManagerService(
            users, actor, CreateEmailService(context, actor), accounts,
            new PermissionRepository(context, actor),
            new AccountAccessService(users, accounts, actor),
            NullLogger<AccountUserManagerService>.Instance);
    }

    private static EmailService CreateEmailService(PlanarianDbContext context, RequestUser actor,
        string? fromDomain = null)
    {
        var options = new EmailOptions { Domain = "example.test", FromDomain = fromDomain };
        var serverOptions = new ServerOptions
        {
            SupportName = "Example Support & Assistance",
            SupportEmail = "help@example.org"
        };
        return new EmailService(
            actor, new NeverSendEmailFactory(), new ClientUrlBuilder(new TestClientRequestOrigin()),
            new MessageLogRepository(context, actor), options, serverOptions,
            new TestHostEnvironment(), NullLogger<EmailService>.Instance);
    }

    private static async Task<Planarian.Modules.Account.Model.InviteUserResultVm> Invite(
        string connectionString, string accountId, string email)
    {
        await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
        var actor = new RequestUser(context) { Id = AdministratorId, AccountId = accountId };
        context.RequestUser = actor;
        var service = CreateManager(context, actor);

        return await service.InviteUser(new InviteUserRequest
        {
            FirstName = "Jane", LastName = "Smith", EmailAddress = email
        }, CancellationToken.None);
    }

    private static async Task SeedAccounts(string connectionString)
    {
        await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
        var actor = new RequestUser(context) { Id = AdministratorId };
        context.RequestUser = actor;
        context.Users.Add(new User("Admin", "User", "admin@example.test") { Id = AdministratorId });
        await context.SaveChangesAsync();

        foreach (var accountId in new[] { AccountId, OtherAccountId })
        {
            actor.AccountId = accountId;
            context.Accounts.Add(new Account
            {
                Id = accountId, Name = accountId,
                DefaultViewAccessAllCaves = false
            });
            context.AccountUsers.Add(new AccountUser
            {
                AccountId = accountId, UserId = AdministratorId,
                InvitationAcceptedOn = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }
    }

    private sealed class NeverSendEmailFactory : IEmailMessageFactory
    {
        public IEmailMessageCore Create() => throw new InvalidOperationException(
            "Isolated account-invitation tests cannot submit real mail.");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Planarian.Tests.EmailDelivery";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestClientRequestOrigin : IClientRequestOrigin
    {
        public string GetOrigin() => "https://planarian.example.test";
    }
}
