using System.Text.Json;
using HandlebarsDotNet;
using Planarian.Library.Exceptions;
using Planarian.Library.Options;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Shared.Base;
using Planarian.Shared.Email.Models;
using Planarian.Shared.Email.Substitutions;
using Planarian.Shared.Options;
using Planarian.Shared.Services;
using Southport.Messaging.Email.Core;
using Southport.Messaging.Email.MailGun;

namespace Planarian.Shared.Email.Services;

public class EmailService : ServiceBase
{
    private const string SenderName = "Planarian";
    private const string SenderLocalPart = "notifications";

    private readonly IEmailMessageFactory _emailMessageFactory;
    private readonly ClientUrlBuilder _clientUrlBuilder;
    private readonly MessageLogRepository _messageLogRepository;
    private readonly EmailOptions _emailOptions;
    private readonly ServerOptions _serverOptions;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<EmailService> _logger;

    public EmailService(RequestUser requestUser,
        IEmailMessageFactory emailMessageFactory, ClientUrlBuilder clientUrlBuilder,
        MessageLogRepository messageLogRepository, EmailOptions emailOptions, ServerOptions serverOptions,
        IHostEnvironment hostEnvironment, ILogger<EmailService> logger) : base(requestUser)
    {
        _emailMessageFactory = emailMessageFactory;
        _clientUrlBuilder = clientUrlBuilder;
        _messageLogRepository = messageLogRepository;
        _emailOptions = emailOptions;
        _serverOptions = serverOptions;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    private async Task<EmailSendResult> SendGenericEmail(MessagePurpose purpose, string subject, string toEmailAddress,
        string toName, GenericEmailSubstitutions substitutions,
        AccountUser? accountInvitation = null,
        Func<string, CancellationToken, Task<bool>>? beforeSend = null,
        CancellationToken cancellationToken = default)
    {
        MessageLog? messageLog = null;
        var messageLogPersisted = false;
        var providerSubmissionSucceeded = false;

        try
        {
            var websiteUrl = _clientUrlBuilder.GetOrigin();
            substitutions.Substitutions["websiteUrl"] = websiteUrl;
            var html = Handlebars.Compile(GenericEmailTemplate.Html)(substitutions.Substitutions);
            var senderDomain = string.IsNullOrWhiteSpace(_emailOptions.FromDomain)
                ? _emailOptions.Domain
                : _emailOptions.FromDomain.Trim();
            var senderAddress = $"{SenderLocalPart}@{senderDomain}";
            var providerCorrelationId = IdGenerator.Generate();

            messageLog = new MessageLog(TemplateKeyConstant.GenericEmail, MessageTypeKeyConstant.Email, purpose,
                MessageProvider.Mailgun, _emailOptions.Domain, providerCorrelationId, subject, toEmailAddress, toName,
                SenderName, senderAddress,
                MessageLogSubstitutionSerializer.Serialize(substitutions.Substitutions), accountInvitation);
            await _messageLogRepository.Create(messageLog, cancellationToken);
            messageLogPersisted = true;

            if (beforeSend != null && !await beforeSend(messageLog.Id, CancellationToken.None))
            {
                await _messageLogRepository.Delete(messageLog, CancellationToken.None);
                return new EmailSendResult(null, null);
            }

            var message = _emailMessageFactory.Create()
                .SetFromAddress(senderAddress, SenderName)
                .SetHtml(html)
                .SetText(substitutions.ToPlainText(websiteUrl))
                .SetSubject(subject)
                .AddToAddress(toEmailAddress, toName)
                .SetTrackingClicks(false)
                .AddCustomArgument(EmailDeliveryMetadata.MessageIdArgument, providerCorrelationId)
                .AddCustomArgument(EmailDeliveryMetadata.EnvironmentArgument, _hostEnvironment.EnvironmentName);

            if (message is IMailGunMessage mailGunMessage)
            {
                mailGunMessage.SetTag(purpose.ToString());
            }

            var results = (await message.Send()).ToList();
            var failedResults = results.Where(e => !e.IsSuccessful).ToList();
            if (failedResults.Count > 0)
            {
                var failure = string.Join(Environment.NewLine,
                    failedResults.Select(e => e.Message).Where(e => !string.IsNullOrWhiteSpace(e)));
                await _messageLogRepository.MarkSubmissionFailed(messageLog.Id, failure, CancellationToken.None);
                return new EmailSendResult(messageLog.Id, MessageDeliveryStatus.SendFailed);
            }

            providerSubmissionSucceeded = true;
            var providerResponse = results.Select(e => e.Message).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
            var providerMessageId = TryGetProviderMessageId(providerResponse);
            await _messageLogRepository.MarkSubmissionSucceeded(messageLog.Id, providerMessageId, providerResponse,
                CancellationToken.None);
            return new EmailSendResult(messageLog.Id, MessageDeliveryStatus.Submitted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (messageLogPersisted)
                await TryMarkSubmissionFailed(messageLog, "Email submission was canceled.");
            throw;
        }
        catch (Exception exception)
        {
            if (messageLogPersisted && !providerSubmissionSucceeded)
            {
                await TryMarkSubmissionFailed(messageLog, exception.Message);
            }
            _logger.LogError(exception,
                "Email {Purpose} processing failed after the caller may have committed its primary operation.", purpose);

            return new EmailSendResult(
                messageLogPersisted ? messageLog?.Id : null,
                providerSubmissionSucceeded ? MessageDeliveryStatus.Submitted : MessageDeliveryStatus.SendFailed);
        }
    }

    private async Task TryMarkSubmissionFailed(MessageLog? messageLog, string? error)
    {
        if (messageLog == null) return;

        try
        {
            await _messageLogRepository.MarkSubmissionFailed(messageLog.Id, error, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to persist failed email submission state for message log {MessageLogId}.",
                messageLog.Id);
        }
    }

    public async Task SendPasswordResetEmail(string emailAddress, string fullName, string resetCode,
        CancellationToken cancellationToken = default)
    {
        const string message =
            "We have received a request to reset your password for your account. If you did not make this request, please ignore this email. If you did make this request, please click the link below to reset your password. This link will expire in 30 minutes.";

        var link = _clientUrlBuilder.BuildPasswordResetUrl(resetCode);
        var result = await SendGenericEmail(MessagePurpose.PasswordReset, "Planarian Password Reset", emailAddress,
            fullName, new GenericEmailSubstitutions(message, "Password Reset", "Reset Password", link),
            cancellationToken: cancellationToken);
        EnsureSubmitted(result);
    }

    public async Task<EmailSendResult> SendEmailConfirmationEmail(string emailAddress, string fullName,
        string emailConfirmationCode, Func<string, CancellationToken, Task<bool>>? beforeSend = null,
        CancellationToken cancellationToken = default)
    {
        return await SendCommittedOperationEmail(MessagePurpose.EmailConfirmation, async () =>
        {
            var link = _clientUrlBuilder.BuildEmailConfirmationUrl(emailConfirmationCode);
            var paragraphs = new List<string>
            {
                "Welcome to Planarian!",
                "Please confirm your email address by clicking the link below. If you did not sign up for Planarian, please ignore this email."
            };

            return await SendGenericEmail(MessagePurpose.EmailConfirmation, "Confirm your email address", emailAddress,
                fullName, new GenericEmailSubstitutions(paragraphs, "Confirm your email address", "Confirm Email", link),
                beforeSend: beforeSend, cancellationToken: cancellationToken);
        }, cancellationToken);
    }

    public Task<EmailSendResult> SendAccountAccessChangeEmail(User user, string? accountName,
        string? notificationMessage, MessagePurpose purpose, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notificationMessage))
            throw new ArgumentException("A notification message is required.", nameof(notificationMessage));

        var (subject, heading) = purpose switch
        {
            MessagePurpose.AccountAccessDisabled =>
                ($"Your access to {accountName} has been disabled", "Account access disabled"),
            MessagePurpose.AccountAccessRestored =>
                ($"Your access to {accountName} has been restored", "Account access restored"),
            MessagePurpose.AccountMembershipRevoked =>
                ($"Your access to {accountName} has been removed", "Account access removed"),
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };

        return SendCommittedOperationEmail(purpose,
            () => SendGenericEmail(purpose, subject, user.EmailAddress, user.FullName,
                new GenericEmailSubstitutions(notificationMessage.Split('\n'), heading),
                cancellationToken: cancellationToken),
            cancellationToken);
    }

    public async Task<EmailSendResult> SendAccountInvitationEmail(User user, AccountUser accountUser, string? accountName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountUser.InvitationCode))
            throw ApiExceptionDictionary.BadRequest("The user does not have an active invitation.");

        var result = await SendCommittedOperationEmail(MessagePurpose.AccountInvitation, async () =>
        {
            var link = _clientUrlBuilder.BuildInvitationUrl(accountUser.InvitationCode);
            var paragraphs = new List<string>
            {
                $"You have been invited by {accountName} to join Planarian! Please click the link below to create your account and accept the invitation.",
            };

            return await SendGenericEmail(MessagePurpose.AccountInvitation, $"Join {accountName} on Planarian!",
                user.EmailAddress, user.FullName, new GenericEmailSubstitutions(paragraphs,
                    "Welcome!", "Create Account", link), accountInvitation: accountUser,
                cancellationToken: cancellationToken);
        }, cancellationToken);

        if (result.WasSubmitted)
        {
            accountUser.InvitationSentOn = DateTime.UtcNow;
        }

        return result;
    }

    private async Task<EmailSendResult> SendCommittedOperationEmail(MessagePurpose purpose,
        Func<Task<EmailSendResult>> send, CancellationToken cancellationToken)
    {
        try
        {
            return await send();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Email {Purpose} setup failed after the caller may have committed its primary operation.", purpose);
            return new EmailSendResult(null, MessageDeliveryStatus.SendFailed);
        }
    }

    public async Task<EmailSendResult> SendPasswordChangedEmail(string emailAddress, string fullName,
        CancellationToken cancellationToken = default)
    {
        var message = $"Your password was changed. If you did not make this change, please contact {_serverOptions.SupportName} immediately at {_serverOptions.SupportEmail}.";

        var substitutions = new GenericEmailSubstitutions(message, "Password changed");

        return await SendCommittedOperationEmail(MessagePurpose.PasswordChanged,
            () => SendGenericEmail(MessagePurpose.PasswordChanged, "Planarian Password Changed", emailAddress,
                fullName, substitutions,
                cancellationToken: cancellationToken),
            cancellationToken);
    }

    private static string? TryGetProviderMessageId(string? providerResponse)
    {
        if (string.IsNullOrWhiteSpace(providerResponse)) return null;

        try
        {
            using var document = JsonDocument.Parse(providerResponse);
            return document.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void EnsureSubmitted(EmailSendResult result)
    {
        if (!result.WasSubmitted) throw ApiExceptionDictionary.EmailFailedToSend;
    }
}
