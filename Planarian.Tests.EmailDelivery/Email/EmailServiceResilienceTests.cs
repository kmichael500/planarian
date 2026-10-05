using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Shared.Email.Models;
using Planarian.Shared.Email.Services;
using Planarian.Shared.Options;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class EmailServiceResilienceTests
{
    [Fact]
    public async Task ConfirmationUrlSetupFailureBecomesSendFailedResult()
    {
        await using var dbContext = CreateUnconfiguredDatabaseContext();
        var service = CreateService(dbContext, new ThrowingClientRequestOrigin());

        var result = await service.SendEmailConfirmationEmail(
            "person@example.com", "Test Person", "Ab3xZ91Qwe");

        Assert.Equal(MessageDeliveryStatus.SendFailed, result.DeliveryStatus);
        Assert.False(result.WasAttempted);
    }

    [Fact]
    public async Task InvitationUrlSetupFailureBecomesSendFailedResult()
    {
        await using var dbContext = CreateUnconfiguredDatabaseContext();
        var service = CreateService(dbContext, new ThrowingClientRequestOrigin());
        var user = new User("Test", "Person", "person@example.com");
        var accountUser = new AccountUser { InvitationCode = "Ab3xZ91Qwe" };

        var result = await service.SendAccountInvitationEmail(user, accountUser, "Example Account");

        Assert.Equal(MessageDeliveryStatus.SendFailed, result.DeliveryStatus);
        Assert.False(result.WasAttempted);
    }

    [Fact]
    public async Task ConfirmationTrackingSetupFailureBecomesSendFailedResult()
    {
        await using var dbContext = CreateUnconfiguredDatabaseContext();
        var service = CreateService(dbContext, new FixedClientRequestOrigin());

        var result = await service.SendEmailConfirmationEmail(
            "person@example.com", "Test Person", "Ab3xZ91Qwe");

        Assert.Equal(MessageDeliveryStatus.SendFailed, result.DeliveryStatus);
        Assert.False(result.WasAttempted);
    }

    [Fact]
    public async Task PasswordChangedNotificationFailureIsBestEffort()
    {
        await using var dbContext = CreateUnconfiguredDatabaseContext();
        var service = CreateService(dbContext, new FixedClientRequestOrigin());

        await service.SendPasswordChangedEmail("person@example.com", "Test Person");
    }

    [Fact]
    public async Task AccountAccessRevokedEmailLogsTheReasonAndDedicatedPurpose()
    {
        await using var dbContext = CreateInMemoryDatabaseContext();
        var requestUser = new RequestUser(dbContext)
        {
            Id = "admin00001",
            AccountId = "account001",
            FirstName = "Account",
            LastName = "Admin"
        };
        dbContext.RequestUser = requestUser;
        dbContext.MessageTypes.Add(new MessageType
        {
            Key = TemplateKeyConstant.GenericEmail,
            Type = MessageTypeKeyConstant.Email,
            Description = "Generic email",
            FromName = "Planarian",
            FromEmail = "planarian@example.com",
            Html = "{{#each messages}}<p>{{this}}</p>{{/each}}"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, new FixedClientRequestOrigin(), requestUser);
        var user = new User("Test", "Person", "person@example.com");

        var result = await service.SendAccountAccessRevokedEmail(
            user, "Example Survey", "Membership expired.");

        Assert.Equal(MessageDeliveryStatus.SendFailed, result.DeliveryStatus);
        var messageLog = await dbContext.MessageLogs.SingleAsync();
        Assert.Equal(MessagePurpose.AccountAccessRevoked, messageLog.Purpose);
        Assert.Equal("Access to Example Survey was revoked", messageLog.Subject);
        Assert.Contains("Membership expired.", messageLog.Substitutions, StringComparison.Ordinal);
        Assert.Contains("Account access revoked", messageLog.Substitutions, StringComparison.Ordinal);
        Assert.Contains("other organizations in Planarian", messageLog.Substitutions, StringComparison.Ordinal);
        Assert.Contains("View details", messageLog.Substitutions, StringComparison.Ordinal);
        Assert.Contains("https://example.com/user/invitations", messageLog.Substitutions, StringComparison.Ordinal);
        Assert.DoesNotContain("permissions", messageLog.Substitutions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AccountAccessRestoredEmailUsesDedicatedPurposeAndClearSubject()
    {
        await using var dbContext = CreateInMemoryDatabaseContext();
        var requestUser = new RequestUser(dbContext)
        {
            Id = "admin00001",
            AccountId = "account001",
            FirstName = "Account",
            LastName = "Admin"
        };
        dbContext.RequestUser = requestUser;
        dbContext.MessageTypes.Add(new MessageType
        {
            Key = TemplateKeyConstant.GenericEmail,
            Type = MessageTypeKeyConstant.Email,
            Description = "Generic email",
            FromName = "Planarian",
            FromEmail = "planarian@example.com",
            Html = "{{#each messages}}<p>{{this}}</p>{{/each}}"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, new FixedClientRequestOrigin(), requestUser);
        var user = new User("Test", "Person", "person@example.com");

        var result = await service.SendAccountAccessRestoredEmail(user, "Example Survey");

        Assert.Equal(MessageDeliveryStatus.SendFailed, result.DeliveryStatus);
        var messageLog = await dbContext.MessageLogs.SingleAsync();
        Assert.Equal(MessagePurpose.AccountAccessRestored, messageLog.Purpose);
        Assert.Equal("Access to Example Survey was restored", messageLog.Subject);
        Assert.Contains("Account access restored", messageLog.Substitutions, StringComparison.Ordinal);
        Assert.Contains("Open Planarian", messageLog.Substitutions, StringComparison.Ordinal);
        Assert.Contains("https://example.com", messageLog.Substitutions, StringComparison.Ordinal);
        Assert.DoesNotContain("permissions", messageLog.Substitutions, StringComparison.OrdinalIgnoreCase);
    }

    private static PlanarianDbContext CreateUnconfiguredDatabaseContext()
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>().Options;
        return new PlanarianDbContext(options);
    }

    private static PlanarianDbContext CreateInMemoryDatabaseContext()
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestPlanarianDbContext(options);
    }

    private static EmailService CreateService(PlanarianDbContext dbContext, IClientRequestOrigin origin,
        RequestUser? requestUser = null)
    {
        requestUser ??= new RequestUser(dbContext);
        return new EmailService(
            new MessageTypeRepository(dbContext, requestUser, null!),
            requestUser,
            null!,
            new ClientUrlBuilder(origin),
            new MessageLogRepository(dbContext, requestUser),
            new EmailOptions { Domain = "mg.example.com" },
            null!,
            NullLogger<EmailService>.Instance);
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

    private sealed class FixedClientRequestOrigin : IClientRequestOrigin
    {
        public string GetOrigin() => "https://example.com";
    }

    private sealed class ThrowingClientRequestOrigin : IClientRequestOrigin
    {
        public string GetOrigin() => throw new InvalidOperationException("Client origin unavailable.");
    }
}
