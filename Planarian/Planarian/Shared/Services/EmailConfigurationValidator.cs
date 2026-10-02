using Planarian.Shared.Options;

namespace Planarian.Shared.Services;

public static class EmailConfigurationValidator
{
    public static void Validate(EmailOptions emailOptions, bool isHostedDeployment)
    {
        if (!isHostedDeployment) return;

        if (string.IsNullOrWhiteSpace(emailOptions.Domain))
            throw new InvalidOperationException("Hosted deployments require Email:Domain for Mailgun delivery tracking.");

        if (string.IsNullOrWhiteSpace(emailOptions.WebhookSigningKey))
            throw new InvalidOperationException(
                "Hosted deployments require Email:WebhookSigningKey so Mailgun webhooks can be authenticated.");
    }
}
