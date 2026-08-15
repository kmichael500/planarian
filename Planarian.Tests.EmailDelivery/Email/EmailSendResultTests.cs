using Planarian.Model.Shared;
using Planarian.Shared.Email.Models;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class EmailSendResultTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(MessageDeliveryStatus.Submitting, false)]
    [InlineData(MessageDeliveryStatus.SendFailed, false)]
    [InlineData(MessageDeliveryStatus.Submitted, true)]
    [InlineData(MessageDeliveryStatus.Accepted, true)]
    [InlineData(MessageDeliveryStatus.TemporaryFailed, true)]
    [InlineData(MessageDeliveryStatus.Delivered, true)]
    [InlineData(MessageDeliveryStatus.PermanentFailed, true)]
    public void WasSubmittedOnlyIncludesStatesThatProveProviderSubmission(
        MessageDeliveryStatus? status, bool expected)
    {
        var result = new EmailSendResult("message123", status);

        Assert.Equal(expected, result.WasSubmitted);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("message123", true)]
    public void WasAttemptedRequiresDurableMessageLogId(string? messageLogId, bool expected)
    {
        var result = new EmailSendResult(messageLogId, MessageDeliveryStatus.Submitting);

        Assert.Equal(expected, result.WasAttempted);
    }
}
