using Planarian.Shared.Options;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.Unit.Email;

public sealed class EmailConfigurationValidatorTests
{
    [Theory]
    [InlineData(null, "mg.example.com")]
    [InlineData("", "mg.example.com")]
    [InlineData("signing-key", null)]
    [InlineData("signing-key", "")]
    public void HostedDeploymentRequiresWebhookConfiguration(string? signingKey, string? domain)
    {
        var options = new EmailOptions { WebhookSigningKey = signingKey, Domain = domain! };

        Assert.Throws<InvalidOperationException>(() =>
            EmailConfigurationValidator.Validate(options, isHostedDeployment: true));
    }

    [Fact]
    public void LocalDevelopmentMayOmitWebhookSigningKey()
    {
        var options = new EmailOptions { Domain = "mg.example.com" };

        EmailConfigurationValidator.Validate(options, isHostedDeployment: false);
    }
}
