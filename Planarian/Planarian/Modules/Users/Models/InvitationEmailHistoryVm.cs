using Planarian.Model.Shared;

namespace Planarian.Modules.Users.Models;

public class InvitationEmailAttemptVm
{
    public string MessageLogId { get; set; } = null!;
    public DateTime CreatedOn { get; set; }
    public MessageDeliveryStatus? DeliveryStatus { get; set; }
    public DateTime? DeliveryStatusOn { get; set; }
    public List<InvitationEmailEventVm> Events { get; set; } = new();
}

public class InvitationEmailEventVm
{
    public MessageDeliveryEventType EventType { get; set; }
    public DateTime OccurredOn { get; set; }
    public string? Bot { get; set; }
    public string? Severity { get; set; }
    public string? Reason { get; set; }
    public string? DeliveryCode { get; set; }
    public string? EnhancedDeliveryCode { get; set; }
    public int? AttemptNumber { get; set; }
    public bool? IsDelayedBounce { get; set; }
}
