using Planarian.Shared.Email.Models;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class EmailDeliveryMetadataTests
{
    [Fact]
    public void CorrelationMetadataUsesStableMailgunUserVariableKey()
    {
        Assert.Equal("planarian-message-id", EmailDeliveryMetadata.MessageIdArgument);
    }
}
