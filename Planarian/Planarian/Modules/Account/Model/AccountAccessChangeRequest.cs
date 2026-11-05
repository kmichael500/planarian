namespace Planarian.Modules.Account.Model;

public enum AccountAccessChangeAction
{
    Disable,
    Enable,
    Revoke
}

public sealed class AccountAccessChangeRequest
{
    public AccountAccessChangeAction Action { get; set; }
    public bool SendEmail { get; set; } = true;
    public string? NotificationMessage { get; set; }
}
