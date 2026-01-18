using Southport.Messaging.Email.MailGun;

namespace Planarian.Shared.Options;

public class EmailOptions : MailGunOptions
{
    public const string Key = "Email";

    public string MjmlApplicationId { get; set; } = null!;
    public string MjmlSecretKey { get; set; } = null!;
}