using Planarian.Model.Shared;

namespace Planarian.Shared.Email.Models;

public record EmailSendResult(string? MessageLogId, MessageDeliveryStatus? DeliveryStatus)
{
    public bool WasAttempted => !string.IsNullOrWhiteSpace(MessageLogId);
    public bool WasSubmitted => DeliveryStatus is MessageDeliveryStatus.Submitted or
        MessageDeliveryStatus.Accepted or MessageDeliveryStatus.TemporaryFailed or
        MessageDeliveryStatus.Delivered or MessageDeliveryStatus.PermanentFailed;
}
