using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Model;

public sealed class RevokeMembershipResultVm
{
    public MessageDeliveryStatus? NotificationEmailDeliveryStatus { get; set; }
}
