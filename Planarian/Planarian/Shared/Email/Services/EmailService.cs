using HandlebarsDotNet;
using Newtonsoft.Json;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Shared.Base;
using Planarian.Shared.Email.Services;
using Planarian.Shared.Email.Substitutions;
using Planarian.Shared.Exceptions;
using Southport.Messaging.Email.Core;

namespace Planarian.Shared.Email;

public class EmailService : ServiceBase<MessageTypeRepository>
{
    private readonly IEmailMessageFactory _emailMessageFactory;
    
    public async Task SendGenericEmail(string subject, string toEmailAddress, string toName,
        GenericEmailSubstitutions substitutions)
    {
        var messageType = await Repository.GetMessageTypeVm(MessageKey.GenericEmail, MessageTypeKey.Email);

        if (messageType == null)
        {
            throw ApiExceptionDictionary.MessageTypeNotFound;
        }

        var html = Handlebars.Compile(messageType.Html)(substitutions.Substitutions);

        var results = await _emailMessageFactory.Create()
            .SetFromAddress(messageType.FromEmail, messageType.FromName)
            .SetHtml(html)
            .SetSubject(subject)
            .AddToAddress(toEmailAddress, toName)
            .Send();

        if (results.Any(e => !e.IsSuccessful))
        {
            throw ApiExceptionDictionary.EmailFailedToSend;
        }

        var messageLog = new MessageLog(MessageKey.GenericEmail, MessageTypeKey.Email, subject, toEmailAddress, toName,
            messageType.FromName, messageType.FromEmail, JsonConvert.SerializeObject(substitutions.Substitutions));

        Repository.Add(messageLog);

        await Repository.SaveChangesAsync();
    }

    public EmailService(MessageTypeRepository repository, RequestUser requestUser,
        IEmailMessageFactory emailMessageFactory) : base(repository, requestUser)
    {
        _emailMessageFactory = emailMessageFactory;
    }
}

public class MessageKey
{
    public const string GenericEmail = "GenericEmail";
}

public class MessageTypeKey
{
    public const string Email = "Email";
    public const string Sms = "Sms";
}