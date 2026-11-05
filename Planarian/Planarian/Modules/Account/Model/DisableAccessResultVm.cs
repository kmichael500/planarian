using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Model;

public sealed class DisableAccessResultVm
{
    public MessageDeliveryStatus? NotificationEmailDeliveryStatus { get; set; }
}
