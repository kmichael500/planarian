using HandlebarsDotNet;
using Newtonsoft.Json;
using Planarian.Library.Options;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Shared.Base;
using Planarian.Shared.Email.Models;
using Planarian.Shared.Email.Substitutions;
using Planarian.Shared.Exceptions;
using Southport.Messaging.Email.Core;

namespace Planarian.Shared.Email.Services;

public class EmailService : ServiceBase<MessageTypeRepository>
{
    private readonly IEmailMessageFactory _emailMessageFactory;
    private readonly ServerOptions _serverOptions;

    public EmailService(MessageTypeRepository repository, RequestUser requestUser,
        IEmailMessageFactory emailMessageFactory, ServerOptions serverOptions) : base(repository, requestUser)
    {
        _emailMessageFactory = emailMessageFactory;
        _serverOptions = serverOptions;
    }

    public async Task SendGenericEmail(string subject, string toEmailAddress, string toName,
        GenericEmailSubstitutions substitutions)
    {
        var messageType =
            await Repository.GetMessageTypeVm(MessageKeyConstant.GenericEmail, MessageTypeKeyConstant.Email);

        if (messageType == null) throw ApiExceptionDictionary.MessageTypeNotFound;

        var html = Handlebars.Compile(messageType.Html)(substitutions.Substitutions);

        var results = await _emailMessageFactory.Create()
            .SetFromAddress(messageType.FromEmail, messageType.FromName)
            .SetHtml(html)
            .SetSubject(subject)
            .AddToAddress(toEmailAddress, toName)
            .Send();

        if (results.Any(e => !e.IsSuccessful)) throw ApiExceptionDictionary.EmailFailedToSend;

        var messageLog = new MessageLog(MessageKeyConstant.GenericEmail, MessageTypeKeyConstant.Email, subject,
            toEmailAddress, toName,
            messageType.FromName, messageType.FromEmail, JsonConvert.SerializeObject(substitutions.Substitutions));

        Repository.Add(messageLog);

        await Repository.SaveChangesAsync();
    }

    public async Task SendPasswordResetEmail(string emailAddress, string fullName, string resetCode)
    {
        const string message =
            "We have received a request to reset your password for your account. If you did not make this request, please ignore this email. If you did make this request, please click the link below to reset your password. This link will expire in 30 minutes.";

        var link = $"{_serverOptions.ClientBaseUrl}/reset-password?code={resetCode}";

        await SendGenericEmail("Planarian Password Reset", emailAddress, fullName,
            new GenericEmailSubstitutions(message, "Password Reset", "Reset Password", link));
    }

    public async Task SendEmailConfirmationEmail(string emailAddress, string fullName, string emailConfirmationCode)
    {
        var link = $"{_serverOptions.ClientBaseUrl}/confirm-email?code={emailConfirmationCode}";

        var paragraphs = new List<string>
        {
            "Welcome to Planarian!",
            "Please confirm your email address by clicking the link below. If you did not sign up for Planarian, please ignore this email."
        };

        await SendGenericEmail("Confirm your email address", emailAddress, fullName,
            new GenericEmailSubstitutions(paragraphs,
                "Confirm your email address", "Confirm Email", link));
    }

    public async Task SendPasswordChangedEmail(string emailAddress, string fullName)
    {
        const string message =
            "You're password was just changed. If you did not make this request, please contact us immediately.";

        await SendGenericEmail("Planarian Password Changed", emailAddress, fullName,
            new GenericEmailSubstitutions(message, "Planarian Password Changed"));
    }
}