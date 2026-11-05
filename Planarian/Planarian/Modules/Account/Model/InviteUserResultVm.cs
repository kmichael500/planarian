using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Model;

public class InviteUserResultVm
{
    public string UserId { get; set; } = null!;
    public bool AlreadyInvited { get; set; }
    public MessageDeliveryStatus? InvitationEmailDeliveryStatus { get; set; }
}
