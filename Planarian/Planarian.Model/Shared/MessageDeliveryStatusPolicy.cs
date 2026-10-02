namespace Planarian.Model.Shared;

public static class MessageDeliveryStatusPolicy
{
    public static bool ShouldApply(
        MessageDeliveryStatus? currentStatus,
        DateTime? currentStatusOn,
        MessageDeliveryStatus incomingStatus,
        DateTime incomingStatusOn)
    {
        if (IsProviderStatus(currentStatus) && currentStatusOn.HasValue && incomingStatusOn < currentStatusOn.Value)
            return false;

        return currentStatus switch
        {
            null or MessageDeliveryStatus.Submitting or MessageDeliveryStatus.Submitted or
                MessageDeliveryStatus.SendFailed => true,
            MessageDeliveryStatus.Accepted => incomingStatus is MessageDeliveryStatus.Accepted or
                MessageDeliveryStatus.TemporaryFailed or MessageDeliveryStatus.Delivered or
                MessageDeliveryStatus.PermanentFailed,
            MessageDeliveryStatus.TemporaryFailed => incomingStatus is MessageDeliveryStatus.TemporaryFailed or
                MessageDeliveryStatus.Delivered or MessageDeliveryStatus.PermanentFailed,
            MessageDeliveryStatus.Delivered => incomingStatus is MessageDeliveryStatus.Delivered or
                MessageDeliveryStatus.PermanentFailed,
            MessageDeliveryStatus.PermanentFailed => incomingStatus == MessageDeliveryStatus.PermanentFailed,
            _ => false
        };
    }

    private static bool IsProviderStatus(MessageDeliveryStatus? status)
    {
        return status is MessageDeliveryStatus.Accepted or MessageDeliveryStatus.TemporaryFailed or
            MessageDeliveryStatus.Delivered or MessageDeliveryStatus.PermanentFailed;
    }
}
