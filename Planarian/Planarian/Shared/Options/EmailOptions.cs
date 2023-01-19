using Southport.Messaging.Email.SendGrid.Interfaces;

namespace Planarian.Shared.Options;

public class EmailOptions : SendGridOptions
{
    public const string Key = "Email";

    public string MjmlApplicationId { get; set; } = null!;
    public string MjmlSecretKey { get; set; } = null!;
}