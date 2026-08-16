using Planarian.Shared.Email.Models;
using Xunit;

namespace Planarian.Tests.Unit.Email;

public sealed class EmailDeliveryMetadataTests
{
    [Fact]
    public void MailgunMetadataUsesStableUserVariableKeys()
    {
        Assert.Equal("planarian-message-id", EmailDeliveryMetadata.MessageIdArgument);
        Assert.Equal("planarian-environment", EmailDeliveryMetadata.EnvironmentArgument);
    }
}
