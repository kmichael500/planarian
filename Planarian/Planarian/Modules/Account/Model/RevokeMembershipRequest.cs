namespace Planarian.Modules.Account.Model;

public sealed class RevokeMembershipRequest
{
    public bool SendEmail { get; set; } = true;
    public string? NotificationMessage { get; set; }
}
