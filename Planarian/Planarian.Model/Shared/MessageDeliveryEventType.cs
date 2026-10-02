namespace Planarian.Model.Shared;

public enum MessageDeliveryEventType
{
    Unknown,
    Accepted,
    TemporaryFailed,
    Delivered,
    PermanentFailed,
    Opened,
    Clicked,
    Unsubscribed,
    Complained
}
