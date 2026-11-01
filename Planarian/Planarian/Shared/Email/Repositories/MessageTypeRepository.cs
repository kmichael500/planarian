using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Shared.Base;
using Planarian.Shared.Email.Models;

namespace Planarian.Shared.Email.Repositories;

public sealed record PendingMessageTypeHtml(string Id, string Mjml);

public class MessageTypeRepository : RepositoryBase
{
    public MessageTypeRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public Task<List<PendingMessageTypeHtml>> GetPendingHtmlAsync(CancellationToken cancellationToken = default) =>
        DbContext.MessageTypes.AsNoTracking()
            .Where(messageType => string.IsNullOrWhiteSpace(messageType.Html) &&
                                  !string.IsNullOrWhiteSpace(messageType.Mjml))
            .Select(messageType => new PendingMessageTypeHtml(messageType.Id, messageType.Mjml!))
            .ToListAsync(cancellationToken);

    public async Task SaveRenderedHtmlAsync(IReadOnlyDictionary<string, string> htmlByMessageTypeId,
        CancellationToken cancellationToken = default)
    {
        if (htmlByMessageTypeId.Count == 0) return;

        var ids = htmlByMessageTypeId.Keys.ToArray();
        var messageTypes = await DbContext.MessageTypes
            .Where(messageType => ids.Contains(messageType.Id) && string.IsNullOrWhiteSpace(messageType.Html))
            .ToListAsync(cancellationToken);

        foreach (var messageType in messageTypes)
            if (htmlByMessageTypeId.TryGetValue(messageType.Id, out var html))
                messageType.Html = html;

        await SaveChangesAsync(cancellationToken);
    }

    public Task<MessageTypeVm?> GetMessageTypeVm(string templateKey, string messageType,
        CancellationToken cancellationToken = default) =>
        DbContext.MessageTypes
            .Where(entity => entity.Key == templateKey && entity.Type == messageType)
            .Select(entity => new MessageTypeVm(entity.Subject!, entity.Html!, entity.FromEmail, entity.FromName))
            .FirstOrDefaultAsync(cancellationToken);
}
