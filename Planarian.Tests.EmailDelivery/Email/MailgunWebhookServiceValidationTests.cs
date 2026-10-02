using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Planarian.Shared.Email.Models;
using Planarian.Shared.Email.Services;
using Planarian.Shared.Options;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class MailgunWebhookServiceValidationTests
{
    private const string SigningKey = "test-signing-key";
    private const string Domain = "mg.example.com";
    private const string Token = "12345678901234567890123456789012345678901234567890";

    [Fact]
    public async Task ValidPrimarySignatureAcceptsUncorrelatedLegacyEvent()
    {
        var payload = CreatePayload();
        payload.Signature!.Signature = Sign(payload.Signature.Timestamp!, Token);

        Assert.Equal(MailgunWebhookProcessingResult.Accepted, await CreateService().Process(payload));
    }

    [Fact]
    public async Task ValidParentSignatureAcceptsUncorrelatedLegacyEvent()
    {
        var payload = CreatePayload();
        payload.Signature!.Signature = null;
        payload.Signature.ParentSignature = Sign(payload.Signature.Timestamp!, Token);

        Assert.Equal(MailgunWebhookProcessingResult.Accepted, await CreateService().Process(payload));
    }

    [Fact]
    public async Task InvalidSignatureIsRejectedBeforeEventProcessing()
    {
        var payload = CreatePayload();
        payload.Signature!.Signature = new string('0', 64);

        Assert.Equal(MailgunWebhookProcessingResult.Rejected, await CreateService().Process(payload));
    }

    [Fact]
    public async Task StaleSignatureIsRejected()
    {
        var timestamp = DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeSeconds().ToString();
        var payload = CreatePayload(timestamp);
        payload.Signature!.Signature = Sign(timestamp, Token);

        Assert.Equal(MailgunWebhookProcessingResult.Rejected, await CreateService().Process(payload));
    }

    [Fact]
    public async Task FutureSignatureOutsideFreshnessWindowIsRejected()
    {
        var timestamp = DateTimeOffset.UtcNow.AddDays(2).ToUnixTimeSeconds().ToString();
        var payload = CreatePayload(timestamp);
        payload.Signature!.Signature = Sign(timestamp, Token);

        Assert.Equal(MailgunWebhookProcessingResult.Rejected, await CreateService().Process(payload));
    }

    [Fact]
    public async Task MalformedHexSignatureIsRejected()
    {
        var payload = CreatePayload();
        payload.Signature!.Signature = "not-hex";

        Assert.Equal(MailgunWebhookProcessingResult.Rejected, await CreateService().Process(payload));
    }

    [Fact]
    public async Task WrongLengthWebhookTokenIsRejected()
    {
        var payload = CreatePayload();
        payload.Signature!.Token = "too-short";
        payload.Signature.Signature = Sign(payload.Signature.Timestamp!, payload.Signature.Token);

        Assert.Equal(MailgunWebhookProcessingResult.Rejected, await CreateService().Process(payload));
    }

    [Fact]
    public async Task MissingSigningKeyIsRejectedRatherThanPretendingDeliveryEventsWillRetry()
    {
        var payload = CreatePayload();
        var service = CreateService(signingKey: null);

        Assert.Equal(MailgunWebhookProcessingResult.Rejected, await service.Process(payload));
    }

    [Fact]
    public async Task MissingConfiguredDomainIsRejectedRatherThanPretendingDeliveryEventsWillRetry()
    {
        var payload = CreatePayload();
        payload.Signature!.Signature = Sign(payload.Signature.Timestamp!, Token);
        var options = new EmailOptions { WebhookSigningKey = SigningKey, Domain = "" };

        Assert.Equal(
            MailgunWebhookProcessingResult.Rejected,
            await CreateService(options).Process(payload));
    }

    [Fact]
    public async Task MissingEventDomainIsRejected()
    {
        var payload = CreatePayload();
        payload.EventData!.Domain = null;
        payload.Signature!.Signature = Sign(payload.Signature.Timestamp!, Token);

        Assert.Equal(MailgunWebhookProcessingResult.Rejected, await CreateService().Process(payload));
    }

    [Fact]
    public async Task AuthenticatedEventForDifferentDomainIsIgnoredWithoutRepositoryLookup()
    {
        var payload = CreatePayload();
        payload.EventData!.Domain!.Name = "other.example.com";
        payload.Signature!.Signature = Sign(payload.Signature.Timestamp!, Token);

        Assert.Equal(MailgunWebhookProcessingResult.Accepted, await CreateService().Process(payload));
    }

    [Fact]
    public async Task AuthenticatedCorrelatedEventForDifferentEnvironmentIsIgnoredWithoutRepositoryLookup()
    {
        var payload = CreatePayload();
        payload.EventData!.UserVariables = JsonDocument.Parse(
            $"{{\"{EmailDeliveryMetadata.MessageIdArgument}\":\"Abc123Def4\",\"{EmailDeliveryMetadata.EnvironmentArgument}\":\"OtherEnvironment\"}}").RootElement.Clone();
        payload.Signature!.Signature = Sign(payload.Signature.Timestamp!, Token);

        Assert.Equal(MailgunWebhookProcessingResult.Accepted, await CreateService().Process(payload));
    }

    [Fact]
    public async Task AuthenticatedCorrelatedEventWithoutEnvironmentMetadataIsIgnoredWithoutRepositoryLookup()
    {
        var payload = CreatePayload();
        payload.EventData!.UserVariables = JsonDocument.Parse(
            $"{{\"{EmailDeliveryMetadata.MessageIdArgument}\":\"Abc123Def4\"}}").RootElement.Clone();
        payload.Signature!.Signature = Sign(payload.Signature.Timestamp!, Token);

        Assert.Equal(MailgunWebhookProcessingResult.Accepted, await CreateService().Process(payload));
    }

    [Fact]
    public async Task ExtremeSignatureTimestampIsRejectedWithoutOverflow()
    {
        var timestamp = long.MaxValue.ToString();
        var payload = CreatePayload(timestamp);
        payload.Signature!.Signature = Sign(timestamp, Token);

        Assert.Equal(MailgunWebhookProcessingResult.Rejected, await CreateService().Process(payload));
    }

    [Fact]
    public async Task ValidParentSignatureCanAuthenticateWhenPrimarySignatureIsInvalid()
    {
        var payload = CreatePayload();
        payload.Signature!.Signature = new string('0', 64);
        payload.Signature.ParentSignature = Sign(payload.Signature.Timestamp!, Token);

        Assert.Equal(MailgunWebhookProcessingResult.Accepted, await CreateService().Process(payload));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.MaxValue)]
    public async Task InvalidProviderEventTimestampIsRejected(double eventTimestamp)
    {
        var payload = CreatePayload();
        payload.EventData!.Timestamp = eventTimestamp;
        payload.Signature!.Signature = Sign(payload.Signature.Timestamp!, Token);

        Assert.Equal(MailgunWebhookProcessingResult.Rejected, await CreateService().Process(payload));
    }

    [Theory]
    [InlineData(null, "provider-event-1")]
    [InlineData("", "provider-event-1")]
    [InlineData("delivered", null)]
    [InlineData("delivered", "")]
    public async Task MissingRequiredProviderEventIdentityIsRejected(string? eventName, string? eventId)
    {
        var payload = CreatePayload();
        payload.EventData!.Event = eventName;
        payload.EventData.Id = eventId;
        payload.Signature!.Signature = Sign(payload.Signature.Timestamp!, Token);

        Assert.Equal(MailgunWebhookProcessingResult.Rejected, await CreateService().Process(payload));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("Abc123Def!")]
    [InlineData("Abc123Def45")]
    public async Task InvalidCorrelationMetadataIsIgnoredWithoutRepositoryLookup(string correlationId)
    {
        var payload = CreatePayload();
        payload.EventData!.UserVariables = JsonDocument.Parse(
            $"{{\"{EmailDeliveryMetadata.MessageIdArgument}\":\"{correlationId}\"}}").RootElement.Clone();
        payload.Signature!.Signature = Sign(payload.Signature.Timestamp!, Token);

        Assert.Equal(MailgunWebhookProcessingResult.Accepted, await CreateService().Process(payload));
    }

    [Fact]
    public async Task ConfiguredDomainComparisonIsTrimmedAndCaseInsensitive()
    {
        var payload = CreatePayload();
        payload.Signature!.Signature = Sign(payload.Signature.Timestamp!, Token);
        var options = new EmailOptions
        {
            Domain = "  MG.EXAMPLE.COM  ",
            WebhookSigningKey = SigningKey
        };

        Assert.Equal(
            MailgunWebhookProcessingResult.Accepted,
            await CreateService(options).Process(payload));
    }

    private static MailgunWebhookService CreateService(string? signingKey = SigningKey,
        string? environmentName = null)
    {
        var options = new EmailOptions
        {
            Domain = Domain,
            WebhookSigningKey = signingKey
        };
        return CreateService(options, environmentName);
    }

    private static MailgunWebhookService CreateService(EmailOptions options,
        string? environmentName = null)
    {
        return new MailgunWebhookService(options, null!, new TestHostEnvironment { EnvironmentName = environmentName ?? Environments.Development });
    }

    private static MailgunWebhookVm CreatePayload(string? signatureTimestamp = null)
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = signatureTimestamp ?? now.ToUnixTimeSeconds().ToString();
        return new MailgunWebhookVm
        {
            Signature = new MailgunWebhookSignatureVm
            {
                Timestamp = timestamp,
                Token = Token
            },
            EventData = new MailgunWebhookEventVm
            {
                Event = "delivered",
                Id = "provider-event-1",
                Timestamp = now.ToUnixTimeMilliseconds() / 1000d,
                Domain = new MailgunWebhookDomainVm { Name = Domain },
                UserVariables = JsonDocument.Parse("{}").RootElement.Clone()
            }
        };
    }

    private static string Sign(string timestamp, string token)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SigningKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp + token))).ToLowerInvariant();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
