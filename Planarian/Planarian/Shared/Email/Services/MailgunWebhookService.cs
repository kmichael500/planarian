using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Email.Models;
using Planarian.Shared.Options;

namespace Planarian.Shared.Email.Services;

public enum MailgunWebhookProcessingResult
{
    Accepted,
    Rejected,
    Retry
}

public class MailgunWebhookService
{
    private readonly EmailOptions _emailOptions;
    private readonly UserRepository _userRepository;

    public MailgunWebhookService(EmailOptions emailOptions, UserRepository userRepository)
    {
        _emailOptions = emailOptions;
        _userRepository = userRepository;
    }

    public async Task<MailgunWebhookProcessingResult> Process(MailgunWebhookVm? payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_emailOptions.WebhookSigningKey))
            return MailgunWebhookProcessingResult.Retry;

        if (payload?.Signature == null || !VerifySignature(payload.Signature))
            return MailgunWebhookProcessingResult.Rejected;

        var eventData = payload.EventData;
        if (eventData == null || !string.Equals(eventData.Event, "failed", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(eventData.Severity, "permanent", StringComparison.OrdinalIgnoreCase))
            return MailgunWebhookProcessingResult.Accepted;

        if (eventData.Tags?.Any(tag => string.Equals(tag, EmailDeliveryMetadata.EmailConfirmationMessageType,
                StringComparison.OrdinalIgnoreCase)) != true)
            return MailgunWebhookProcessingResult.Accepted;

        if (string.IsNullOrWhiteSpace(eventData.Recipient) ||
            !TryGetUserVariable(eventData.UserVariables, EmailDeliveryMetadata.MessageTypeArgument, out var messageType) ||
            !string.Equals(messageType, EmailDeliveryMetadata.EmailConfirmationMessageType,
                StringComparison.OrdinalIgnoreCase) ||
            !TryGetUserVariable(eventData.UserVariables, EmailDeliveryMetadata.DeliveryIdArgument, out var deliveryId) ||
            string.IsNullOrWhiteSpace(deliveryId))
            return MailgunWebhookProcessingResult.Accepted;

        var user = await _userRepository.GetUserByEmail(eventData.Recipient);
        if (user == null)
            return MailgunWebhookProcessingResult.Accepted;

        if (user.EmailConfirmedOn != null ||
            !string.Equals(user.EmailConfirmationDeliveryId, deliveryId, StringComparison.Ordinal))
            return MailgunWebhookProcessingResult.Accepted;

        var failedOn = GetEventTimestamp(eventData.Timestamp);
        var recorded = await _userRepository.RecordEmailConfirmationDeliveryFailure(
            eventData.Recipient, deliveryId, failedOn, cancellationToken);

        // A zero-row update means the user confirmed or a newer resend won after the read above.
        // In either case this delivery event is stale and should not be retried.
        return MailgunWebhookProcessingResult.Accepted;
    }

    private bool VerifySignature(MailgunWebhookSignatureVm signature)
    {
        if (string.IsNullOrWhiteSpace(signature.Timestamp) || string.IsNullOrWhiteSpace(signature.Token) ||
            string.IsNullOrWhiteSpace(signature.Signature))
            return false;

        byte[] providedSignature;
        try
        {
            providedSignature = Convert.FromHexString(signature.Signature);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_emailOptions.WebhookSigningKey!));
        var expectedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(signature.Timestamp + signature.Token));
        return providedSignature.Length == expectedSignature.Length &&
               CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature);
    }

    private static bool TryGetUserVariable(JsonElement userVariables, string key, out string? value)
    {
        value = null;
        if (userVariables.ValueKind != JsonValueKind.Object || !userVariables.TryGetProperty(key, out var property) ||
            property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString();
        return true;
    }

    private static DateTime GetEventTimestamp(double timestamp)
    {
        if (!double.IsFinite(timestamp) || timestamp <= 0)
            return DateTime.UtcNow;

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(checked((long)(timestamp * 1000))).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTime.UtcNow;
        }
        catch (OverflowException)
        {
            return DateTime.UtcNow;
        }
    }
}
