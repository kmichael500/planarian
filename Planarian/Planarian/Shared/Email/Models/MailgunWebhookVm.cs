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
}

public class MailgunWebhookEventVm
{
    [JsonPropertyName("event")]
    public string? Event { get; set; }

    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("timestamp")]
    public double Timestamp { get; set; }

    [JsonPropertyName("recipient")]
    public string? Recipient { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("user-variables")]
    public JsonElement UserVariables { get; set; }
}
