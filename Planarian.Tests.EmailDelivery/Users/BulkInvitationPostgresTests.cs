using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
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
    public async Task ExistingMembershipIsNotReinvitedOrReactivated(bool revoked)
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
                    AccessRevokedOn = revoked ? DateTime.UtcNow : null
                });
                await seed.SaveChangesAsync();
            }

            var error = await Assert.ThrowsAsync<ApiException>(() =>
                Invite(connectionString, AccountId, "member@example.test"));
            Assert.Equal(revoked ? 409 : 400, error.StatusCode);

            await using var verify = IsolatedPostgresDatabase.NewContext(connectionString);
            Assert.False(await verify.Users.AnyAsync(u => u.IsTemporary));
            Assert.Single(await verify.AccountUsers.Where(m =>
                m.AccountId == AccountId && m.UserId == memberId &&
                m.AccessRevokedOn.HasValue == revoked).ToListAsync());
        });
    }

    private static async Task<Planarian.Modules.Account.Model.InviteUserResultVm> Invite(
        string connectionString, string accountId, string email)
    {
        await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
        var actor = new RequestUser(context) { Id = AdministratorId, AccountId = accountId };
        context.RequestUser = actor;
        var users = new UserRepository(context, actor);
        var accounts = new AccountRepository(context, actor);
        var emailOptions = new EmailOptions { Domain = "example.test" };
        using var httpClient = new HttpClient();
        var emailService = new EmailService(
            new MessageTypeRepository(context, actor, new MjmlService(httpClient, emailOptions)),
            actor, new NeverSendEmailFactory(),
            new ClientUrlBuilder(new ThrowingClientRequestOrigin()),
            new MessageLogRepository(context, actor),
            emailOptions, new TestHostEnvironment(), NullLogger<EmailService>.Instance);
        var service = new AccountUserManagerService(
            users, actor, emailService, accounts,
            new PermissionRepository(context, actor),
            new AccountAccessService(users, accounts, actor),
            NullLogger<AccountUserManagerService>.Instance);

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

    private sealed class ThrowingClientRequestOrigin : IClientRequestOrigin
    {
        public string GetOrigin() => throw new InvalidOperationException(
            "No email provider should be called in isolated invitation tests.");
    }
}
