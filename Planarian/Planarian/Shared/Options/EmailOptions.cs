using Southport.Messaging.Email.MailGun;

namespace Planarian.Shared.Options;

public class EmailOptions : MailGunOptions
{
    public const string Key = "Email";

    public string? FromDomain { get; set; }
    public string? WebhookSigningKey { get; set; }
}
