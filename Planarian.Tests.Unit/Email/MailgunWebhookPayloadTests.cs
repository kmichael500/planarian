using System.Text.Json;
using Planarian.Shared.Email.Models;
using Xunit;

namespace Planarian.Tests.Unit.Email;

public sealed class MailgunWebhookPayloadTests
{
    [Fact]
    public void FailedEventPayloadMapsProviderFieldsUsedByDeliveryTracking()
    {
        const string json = """
        {
          "signature": {
            "timestamp": "1770000000",
            "token": "12345678901234567890123456789012345678901234567890",
            "signature": "abc",
            "parent-signature": "def"
          },
          "event-data": {
            "event": "failed",
            "id": "provider-event-1",
            "timestamp": 1770000000.125,
            "severity": "temporary",
            "reason": "generic",
            "domain": { "name": "mg.example.com" },
            "message": { "headers": { "message-id": "<message@example.com>" } },
            "delivery-status": {
              "attempt-no": 2,
              "code": 451,
              "enhanced-code": "4.2.0",
              "message": "Temporary failure",
              "is-delayed-bounce": true
            },
            "flags": { "is-delayed-bounce": true },
            "user-variables": { "planarian-message-id": "Abc123Def4" },
            "client-info": { "bot": "scanner" }
          }
        }
        """;

        var payload = JsonSerializer.Deserialize<MailgunWebhookVm>(json);

        Assert.NotNull(payload?.Signature);
        Assert.Equal("def", payload.Signature.ParentSignature);
        Assert.Equal("failed", payload.EventData?.Event);
        Assert.Equal("mg.example.com", payload.EventData?.Domain?.Name);
        Assert.Equal("<message@example.com>", payload.EventData?.Message?.Headers?.MessageId);
        Assert.Equal(2, payload.EventData?.DeliveryStatus?.AttemptNumber);
        Assert.Equal(451, payload.EventData?.DeliveryStatus?.Code.GetInt32());
        Assert.True(payload.EventData?.DeliveryStatus?.IsDelayedBounce);
        Assert.Equal("scanner", payload.EventData?.ClientInfo?.Bot);
        Assert.Equal("Abc123Def4", payload.EventData?.UserVariables.GetProperty("planarian-message-id").GetString());
    }
}
