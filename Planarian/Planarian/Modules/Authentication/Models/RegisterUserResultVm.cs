using Planarian.Model.Shared;

namespace Planarian.Modules.Authentication.Models;

public class RegisterUserResultVm
{
    public MessageDeliveryStatus? ConfirmationEmailDeliveryStatus { get; set; }
}
