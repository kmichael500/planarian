namespace Planarian.Model.Shared;

public enum MessageDeliveryStatus
{
    Submitting,
    Submitted,
    SendFailed,
    Accepted,
    TemporaryFailed,
    Delivered,
    PermanentFailed
}
