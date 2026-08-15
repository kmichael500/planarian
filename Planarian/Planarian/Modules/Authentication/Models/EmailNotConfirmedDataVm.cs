using Planarian.Model.Shared;

namespace Planarian.Modules.Authentication.Models;

public class EmailNotConfirmedDataVm
{
    public MessageDeliveryStatus? ConfirmationEmailDeliveryStatus { get; set; }
}
