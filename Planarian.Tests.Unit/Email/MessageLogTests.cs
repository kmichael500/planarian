using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Xunit;

namespace Planarian.Tests.Unit.Email;

public sealed class MessageLogTests
{
    [Fact]
    public void NewTrackedMessageStartsSubmittingWithProviderCorrelation()
    {
        var before = DateTime.UtcNow;

        var messageLog = CreateMessageLog();

        var after = DateTime.UtcNow;
        Assert.Equal(MessagePurpose.EmailConfirmation, messageLog.Purpose);
        Assert.Equal(MessageProvider.Mailgun, messageLog.Provider);
        Assert.Equal("mg.example.com", messageLog.ProviderDomain);
        Assert.Equal("Abc123Def4", messageLog.ProviderCorrelationId);
        Assert.Equal(MessageDeliveryStatus.Submitting, messageLog.DeliveryStatus);
        Assert.NotNull(messageLog.DeliveryStatusOn);
        Assert.InRange(messageLog.DeliveryStatusOn!.Value, before, after);
        Assert.Null(messageLog.AccountInvitationAccountId);
        Assert.Null(messageLog.AccountInvitationUserId);
    }

    [Fact]
    public void AccountInvitationMessageCapturesStableCompositeInvitationIdentity()
    {
        var invitation = new AccountUser
        {
            AccountId = "account1234",
            UserId = "invitee123"
        };

        var messageLog = CreateMessageLog(invitation);

        Assert.Equal("account1234", messageLog.AccountInvitationAccountId);
        Assert.Equal("invitee123", messageLog.AccountInvitationUserId);
    }

    private static MessageLog CreateMessageLog(AccountUser? invitation = null)
    {
        return new MessageLog(
            "GenericEmail", "Email", MessagePurpose.EmailConfirmation, MessageProvider.Mailgun,
            "mg.example.com", "Abc123Def4", "Subject", "to@example.com", "To Name",
            "Planarian", "from@example.com", "{}", invitation);
    }
}
