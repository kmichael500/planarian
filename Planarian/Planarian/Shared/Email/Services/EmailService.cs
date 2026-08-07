using HandlebarsDotNet;
using Newtonsoft.Json;
using Planarian.Library.Constants;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Shared.Base;
using Planarian.Shared.Email.Models;
using Planarian.Shared.Email.Substitutions;
using Planarian.Shared.Services;
using Southport.Messaging.Email.Core;

namespace Planarian.Shared.Email.Services;

public class EmailService : ServiceBase<MessageTypeRepository>
{
    private readonly IEmailMessageFactory _emailMessageFactory;
    private readonly ClientUrlBuilder _clientUrlBuilder;

    public EmailService(MessageTypeRepository repository, RequestUser requestUser,
        IEmailMessageFactory emailMessageFactory, ClientUrlBuilder clientUrlBuilder) : base(repository, requestUser)
    {
        _emailMessageFactory = emailMessageFactory;
        _clientUrlBuilder = clientUrlBuilder;
    }

    public async Task SendGenericEmail(string subject, string toEmailAddress, string toName,
        GenericEmailSubstitutions substitutions)
    {
        var messageType =
            await Repository.GetMessageTypeVm(MessageKeyConstant.GenericEmail, MessageTypeKeyConstant.Email);

        if (messageType == null) throw ApiExceptionDictionary.MessageTypeNotFound;

        substitutions.Substitutions["websiteUrl"] = _clientUrlBuilder.GetOrigin();
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

        var link = _clientUrlBuilder.BuildPasswordResetUrl(resetCode);

        await SendGenericEmail("Planarian Password Reset", emailAddress, fullName,
            new GenericEmailSubstitutions(message, "Password Reset", "Reset Password", link));
    }

    public async Task SendEmailConfirmationEmail(string emailAddress, string fullName, string emailConfirmationCode)
    {
        var link = _clientUrlBuilder.BuildEmailConfirmationUrl(emailConfirmationCode);

        var paragraphs = new List<string>
        {
            "Welcome to Planarian!",
            "Please confirm your email address by clicking the link below. If you did not sign up for Planarian, please ignore this email."
        };

        await SendGenericEmail("Confirm your email address", emailAddress, fullName,
            new GenericEmailSubstitutions(paragraphs,
                "Confirm your email address", "Confirm Email", link));
    }
    
    public async Task SendAccountInvitationEmail(User user, AccountUser accountUser, string? accountName)
    {
        var link = _clientUrlBuilder.BuildInvitationUrl(accountUser.InvitationCode);
        var paragraphs = new List<string>
        {
            $"You have been invited by {accountName} to join Planarian! Please click the link below to create your account and accept the invitation.",
        };

        await SendGenericEmail($"Join {accountName} on Planarian!", user.EmailAddress, user.FullName,
            new GenericEmailSubstitutions(paragraphs,
                "Welcome!", "Create Account", link));

        accountUser.InvitationSentOn = DateTime.UtcNow;
    }

    public async Task SendPasswordChangedEmail(string emailAddress, string fullName)
    {
        const string message =
            "You're password was just changed. If you did not make this request, please contact us immediately.";

        await SendGenericEmail("Planarian Password Changed", emailAddress, fullName,
            new GenericEmailSubstitutions(message, "Planarian Password Changed"));
    }
}
