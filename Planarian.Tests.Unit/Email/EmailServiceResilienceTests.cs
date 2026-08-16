using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Shared.Email.Services;
using Planarian.Shared.Options;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.Unit.Email;

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

    private static PlanarianDbContext CreateUnconfiguredDatabaseContext()
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>().Options;
        return new PlanarianDbContext(options);
    }

    private static EmailService CreateService(PlanarianDbContext dbContext, IClientRequestOrigin origin)
    {
        var requestUser = new RequestUser(dbContext);
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

    private sealed class FixedClientRequestOrigin : IClientRequestOrigin
    {
        public string GetOrigin() => "https://example.com";
    }

    private sealed class ThrowingClientRequestOrigin : IClientRequestOrigin
    {
        public string GetOrigin() => throw new InvalidOperationException("Client origin unavailable.");
    }
}
