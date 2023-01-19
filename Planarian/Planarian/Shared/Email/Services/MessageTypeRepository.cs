using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Shared.Email.Services;

public class MessageTypeRepository : RepositoryBase
{
    private readonly MjmlService _mjmlService;

    public MessageTypeRepository(PlanarianDbContext dbContext, RequestUser requestUser, MjmlService mjmlService) : base(
        dbContext, requestUser)
    {
        _mjmlService = mjmlService;
    }

    public async Task<MessageTypeVm?> GetMessageTypeVm(string messageKey, string messageType)
    {
        await UpdateHtml();
        var message = await DbContext.MessageTypes.Where(e => e.Key == messageKey && e.Type == messageType)
            .Select(e => new MessageTypeVm(e.Subject, e.Html, e.FromEmail, e.FromName))
            .FirstOrDefaultAsync();

        return message;
    }

    private async Task UpdateHtml()
    {
        var messageTypes = await DbContext.MessageTypes
            .Where(e => string.IsNullOrWhiteSpace(e.Html) && !string.IsNullOrWhiteSpace(e.Mjml)).ToListAsync();

        foreach (var messageType in messageTypes)
        {
            if (messageType.Mjml == null) continue;

            var html = await _mjmlService.MjmlToHtml(messageType.Mjml);
            messageType.Html = html;
        }

        await SaveChangesAsync();
    }
}