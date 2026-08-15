using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planarian.Shared.Email.Models;

public class MailgunWebhookVm
{
    [JsonPropertyName("signature")]
    public MailgunWebhookSignatureVm? Signature { get; set; }

    [JsonPropertyName("event-data")]
    public MailgunWebhookEventVm? EventData { get; set; }
}

public class MailgunWebhookSignatureVm
{
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("parent-signature")]
    public string? ParentSignature { get; set; }
}

public class MailgunWebhookEventVm
{
    [JsonPropertyName("event")]
    public string? Event { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("timestamp")]
    public double Timestamp { get; set; }

    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("recipient")]
    public string? Recipient { get; set; }

    [JsonPropertyName("domain")]
    public MailgunWebhookDomainVm? Domain { get; set; }

    [JsonPropertyName("message")]
    public MailgunWebhookMessageVm? Message { get; set; }

    [JsonPropertyName("delivery-status")]
    public MailgunWebhookDeliveryStatusVm? DeliveryStatus { get; set; }

    [JsonPropertyName("flags")]
    public MailgunWebhookFlagsVm? Flags { get; set; }

    [JsonPropertyName("user-variables")]
    public JsonElement UserVariables { get; set; }

    [JsonPropertyName("client-info")]
    public MailgunWebhookClientInfoVm? ClientInfo { get; set; }

    [JsonPropertyName("is-delayed-bounce")]
    public bool? IsDelayedBounce { get; set; }
}

public class MailgunWebhookDomainVm
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class MailgunWebhookMessageVm
{
    [JsonPropertyName("headers")]
    public MailgunWebhookMessageHeadersVm? Headers { get; set; }
}

public class MailgunWebhookMessageHeadersVm
{
    [JsonPropertyName("message-id")]
    public string? MessageId { get; set; }
}

public class MailgunWebhookDeliveryStatusVm
{
    [JsonPropertyName("attempt-no")]
    public int? AttemptNumber { get; set; }

    [JsonPropertyName("code")]
    public JsonElement Code { get; set; }

    [JsonPropertyName("enhanced-code")]
    public string? EnhancedCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("is-delayed-bounce")]
    public bool? IsDelayedBounce { get; set; }
}

public class MailgunWebhookFlagsVm
{
    [JsonPropertyName("is-delayed-bounce")]
    public bool? IsDelayedBounce { get; set; }
}

public class MailgunWebhookClientInfoVm
{
    [JsonPropertyName("bot")]
    public string? Bot { get; set; }
}
