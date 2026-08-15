using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
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
    private static readonly TimeSpan SignatureAgeLimit = TimeSpan.FromHours(24);
    private readonly EmailOptions _emailOptions;
    private readonly MessageLogRepository _messageLogRepository;
    private readonly IHostEnvironment _hostEnvironment;

    public MailgunWebhookService(EmailOptions emailOptions, MessageLogRepository messageLogRepository,
        IHostEnvironment hostEnvironment)
    {
        _emailOptions = emailOptions;
        _messageLogRepository = messageLogRepository;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<MailgunWebhookProcessingResult> Process(MailgunWebhookVm? payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_emailOptions.WebhookSigningKey))
            return MailgunWebhookProcessingResult.Rejected;

        var configuredDomain = _emailOptions.Domain?.Trim();
        if (string.IsNullOrWhiteSpace(configuredDomain))
            return MailgunWebhookProcessingResult.Rejected;

        if (payload?.Signature == null || !VerifySignature(payload.Signature))
            return MailgunWebhookProcessingResult.Rejected;

        var eventData = payload.EventData;
        if (eventData == null || string.IsNullOrWhiteSpace(eventData.Event) || string.IsNullOrWhiteSpace(eventData.Id) ||
            !TryGetEventTimestamp(eventData.Timestamp, out var occurredOn))
        {
            return MailgunWebhookProcessingResult.Rejected;
        }

        var providerDomain = eventData.Domain?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(providerDomain)) return MailgunWebhookProcessingResult.Rejected;

        if (!string.Equals(providerDomain, configuredDomain, StringComparison.OrdinalIgnoreCase))
            return MailgunWebhookProcessingResult.Accepted;

        if (!TryGetUserVariable(eventData.UserVariables, EmailDeliveryMetadata.MessageIdArgument,
                out var providerCorrelationId) || !IsProviderCorrelationId(providerCorrelationId))
        {
            // Legacy and externally-generated messages have no Planarian correlation metadata.
            return MailgunWebhookProcessingResult.Accepted;
        }

        if (!TryGetUserVariable(eventData.UserVariables, EmailDeliveryMetadata.EnvironmentArgument,
                out var messageEnvironment) ||
            !string.Equals(messageEnvironment, _hostEnvironment.EnvironmentName, StringComparison.Ordinal))
        {
            // The same Mailgun domain can fan out to multiple Planarian environments.
            // Each deployment records only events for messages that it sent.
            return MailgunWebhookProcessingResult.Accepted;
        }

        var classification = MailgunWebhookEventClassifier.Classify(eventData.Event, eventData.Severity);
        var messageEvent = new MessageLogEvent
        {
            Provider = MessageProvider.Mailgun,
            ProviderDomain = providerDomain,
            EventType = classification.EventType,
            OccurredOn = occurredOn,
            ProviderEventType = eventData.Event,
            ProviderEventId = eventData.Id,
            ProviderEventDay = DateOnly.FromDateTime(occurredOn),
            WebhookToken = payload.Signature.Token,
            Severity = eventData.Severity,
            Reason = eventData.Reason,
            DeliveryCode = GetJsonScalar(eventData.DeliveryStatus?.Code),
            EnhancedDeliveryCode = eventData.DeliveryStatus?.EnhancedCode,
            DeliveryMessage = eventData.DeliveryStatus?.Message,
            AttemptNumber = eventData.DeliveryStatus?.AttemptNumber,
            IsDelayedBounce = eventData.IsDelayedBounce ?? eventData.Flags?.IsDelayedBounce ??
                              eventData.DeliveryStatus?.IsDelayedBounce,
            Bot = NormalizeOptional(eventData.ClientInfo?.Bot)
        };

        var result = await _messageLogRepository.RecordProviderEvent(
            providerCorrelationId,
            eventData.Message?.Headers?.MessageId,
            messageEvent,
            classification.DeliveryStatus,
            cancellationToken);

        return result switch
        {
            MessageLogEventRecordResult.Recorded => MailgunWebhookProcessingResult.Accepted,
            MessageLogEventRecordResult.Duplicate => MailgunWebhookProcessingResult.Accepted,
            MessageLogEventRecordResult.MessageNotFound => MailgunWebhookProcessingResult.Accepted,
            _ => MailgunWebhookProcessingResult.Retry
        };
    }

    private bool VerifySignature(MailgunWebhookSignatureVm signature)
    {
        if (string.IsNullOrWhiteSpace(signature.Timestamp) || string.IsNullOrWhiteSpace(signature.Token) ||
            signature.Token.Length != PropertyLength.MailgunWebhookToken ||
            (string.IsNullOrWhiteSpace(signature.Signature) && string.IsNullOrWhiteSpace(signature.ParentSignature)) ||
            !long.TryParse(signature.Timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp) ||
            timestamp <= 0)
        {
            return false;
        }

        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signatureAgeLimitSeconds = checked((long)SignatureAgeLimit.TotalSeconds);
        if (timestamp < currentTimestamp - signatureAgeLimitSeconds ||
            timestamp > currentTimestamp + signatureAgeLimitSeconds)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_emailOptions.WebhookSigningKey!));
        var expectedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(signature.Timestamp + signature.Token));
        return MatchesSignature(signature.Signature, expectedSignature) ||
               MatchesSignature(signature.ParentSignature, expectedSignature);
    }

    private static bool MatchesSignature(string? signature, byte[] expectedSignature)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;

        try
        {
            var providedSignature = Convert.FromHexString(signature);
            return providedSignature.Length == expectedSignature.Length &&
                   CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryGetUserVariable(JsonElement userVariables, string key, out string? value)
    {
        value = null;
        if (userVariables.ValueKind != JsonValueKind.Object || !userVariables.TryGetProperty(key, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool IsProviderCorrelationId([NotNullWhen(true)] string? value)
    {
        return value is { Length: PropertyLength.Id } && value.All(character =>
            character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9');
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool TryGetEventTimestamp(double timestamp, out DateTime occurredOn)
    {
        occurredOn = default;
        if (!double.IsFinite(timestamp) || timestamp <= 0) return false;

        try
        {
            occurredOn = DateTimeOffset.FromUnixTimeMilliseconds(checked((long)(timestamp * 1000))).UtcDateTime;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static string? GetJsonScalar(JsonElement? element)
    {
        if (element == null) return null;

        return element.Value.ValueKind switch
        {
            JsonValueKind.String => element.Value.GetString(),
            JsonValueKind.Number => element.Value.GetRawText(),
            _ => null
        };
    }
}
