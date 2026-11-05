namespace Planarian.Modules.Account.Model;

public sealed class DisableAccessRequest
{
    public string? DisableMessage { get; set; }
    public bool SendEmail { get; set; } = true;
}
