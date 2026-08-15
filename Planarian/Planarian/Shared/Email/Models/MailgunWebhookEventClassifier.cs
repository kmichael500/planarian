using Planarian.Model.Shared;

namespace Planarian.Shared.Email.Models;

public readonly record struct MailgunWebhookEventClassification(
    MessageDeliveryEventType EventType,
    MessageDeliveryStatus? DeliveryStatus);

public static class MailgunWebhookEventClassifier
{
    public static MailgunWebhookEventClassification Classify(string eventName, string? severity)
    {
        var eventType = GetEventType(eventName, severity);
        MessageDeliveryStatus? deliveryStatus = eventType switch
        {
            MessageDeliveryEventType.Accepted => MessageDeliveryStatus.Accepted,
            MessageDeliveryEventType.TemporaryFailed => MessageDeliveryStatus.TemporaryFailed,
            MessageDeliveryEventType.Delivered => MessageDeliveryStatus.Delivered,
            MessageDeliveryEventType.PermanentFailed => MessageDeliveryStatus.PermanentFailed,
            _ => null
        };

        return new MailgunWebhookEventClassification(eventType, deliveryStatus);
    }

    private static MessageDeliveryEventType GetEventType(string eventName, string? severity)
    {
        if (string.Equals(eventName, "failed", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(severity, "permanent", StringComparison.OrdinalIgnoreCase))
                return MessageDeliveryEventType.PermanentFailed;
            if (string.Equals(severity, "temporary", StringComparison.OrdinalIgnoreCase))
                return MessageDeliveryEventType.TemporaryFailed;
            return MessageDeliveryEventType.Unknown;
        }

        return eventName.ToLowerInvariant() switch
        {
            "accepted" => MessageDeliveryEventType.Accepted,
            "delivered" => MessageDeliveryEventType.Delivered,
            "opened" => MessageDeliveryEventType.Opened,
            "clicked" => MessageDeliveryEventType.Clicked,
            "unsubscribed" => MessageDeliveryEventType.Unsubscribed,
            "complained" => MessageDeliveryEventType.Complained,
            _ => MessageDeliveryEventType.Unknown
        };
    }
}
