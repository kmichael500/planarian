using Planarian.Model.Shared;
using Planarian.Shared.Email.Models;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class MailgunWebhookEventClassifierTests
{
    public static TheoryData<string, string?, MessageDeliveryEventType, MessageDeliveryStatus?> EventCases => new()
    {
        { "accepted", null, MessageDeliveryEventType.Accepted, MessageDeliveryStatus.Accepted },
        { "delivered", null, MessageDeliveryEventType.Delivered, MessageDeliveryStatus.Delivered },
        { "failed", "temporary", MessageDeliveryEventType.TemporaryFailed, MessageDeliveryStatus.TemporaryFailed },
        { "failed", "permanent", MessageDeliveryEventType.PermanentFailed, MessageDeliveryStatus.PermanentFailed },
        { "opened", null, MessageDeliveryEventType.Opened, null },
        { "clicked", null, MessageDeliveryEventType.Clicked, null },
        { "unsubscribed", null, MessageDeliveryEventType.Unsubscribed, null },
        { "complained", null, MessageDeliveryEventType.Complained, null },
        { "future-event", null, MessageDeliveryEventType.Unknown, null },
        { "failed", "unknown", MessageDeliveryEventType.Unknown, null }
    };

    [Theory]
    [MemberData(nameof(EventCases))]
    public void ClassifyMapsMailgunEventsWithoutTreatingEngagementAsDeliveryState(
        string eventName, string? severity, MessageDeliveryEventType expectedEventType,
        MessageDeliveryStatus? expectedDeliveryStatus)
    {
        var classification = MailgunWebhookEventClassifier.Classify(eventName, severity);

        Assert.Equal(expectedEventType, classification.EventType);
        Assert.Equal(expectedDeliveryStatus, classification.DeliveryStatus);
    }

    [Fact]
    public void ClassifyIsCaseInsensitiveForProviderValues()
    {
        var classification = MailgunWebhookEventClassifier.Classify("FAILED", "PERMANENT");

        Assert.Equal(MessageDeliveryEventType.PermanentFailed, classification.EventType);
        Assert.Equal(MessageDeliveryStatus.PermanentFailed, classification.DeliveryStatus);
    }
}
