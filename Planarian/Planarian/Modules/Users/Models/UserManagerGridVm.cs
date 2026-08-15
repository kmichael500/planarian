using Planarian.Model.Shared;

namespace Planarian.Modules.Users.Models;

public class UserManagerGridVm
{
    public string UserId { get; set; } = null!;
    public string EmailAddress { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public DateTime? InvitationAcceptedOn { get; set; }
    public DateTime? InvitationSentOn { get; set; }
    public DateTime? LastActiveOn { get; set; }
    public bool HasActiveInvitation { get; set; }
    public int InvitationEmailAttemptCount { get; set; }
    public MessageDeliveryStatus? InvitationEmailDeliveryStatus { get; set; }
    public DateTime? InvitationEmailDeliveryStatusOn { get; set; }
    public int InvitationEmailOpenCount { get; set; }
    public int InvitationEmailAutomatedOpenCount { get; set; }
    public int InvitationEmailClickCount { get; set; }
    public int InvitationEmailAutomatedClickCount { get; set; }
}
