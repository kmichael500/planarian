using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Shared.Base;

namespace Planarian.Shared.Email.Services;

public enum MessageLogEventRecordResult
{
    Recorded,
    Duplicate,
    MessageNotFound
}

public class MessageLogRepository : RepositoryBase
{
    public MessageLogRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task Create(MessageLog messageLog, CancellationToken cancellationToken = default)
    {
        Add(messageLog);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task Delete(MessageLog messageLog, CancellationToken cancellationToken = default)
    {
        Delete((EntityBase)messageLog);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSubmissionSucceeded(string messageLogId, string? providerMessageId,
        string? providerResponse, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(providerMessageId))
        {
            await DbContext.MessageLogs
                .Where(e => e.Id == messageLogId && e.ProviderMessageId == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.ProviderMessageId, providerMessageId), cancellationToken);
        }

        var now = DateTime.UtcNow;
        await DbContext.MessageLogs
            .Where(e => e.Id == messageLogId && e.DeliveryStatus == MessageDeliveryStatus.Submitting)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.DeliveryStatus, MessageDeliveryStatus.Submitted)
                .SetProperty(e => e.DeliveryStatusOn, now)
                .SetProperty(e => e.DeliveryStatusMessage, providerResponse), cancellationToken);
    }

    public async Task MarkSubmissionFailed(string messageLogId, string? error,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await DbContext.MessageLogs
            .Where(e => e.Id == messageLogId && e.DeliveryStatus == MessageDeliveryStatus.Submitting)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.DeliveryStatus, MessageDeliveryStatus.SendFailed)
                .SetProperty(e => e.DeliveryStatusOn, now)
                .SetProperty(e => e.DeliveryStatusMessage, error), cancellationToken);
    }

    public async Task<MessageDeliveryStatus?> GetDeliveryStatus(string? messageLogId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageLogId)) return null;

        return await DbContext.MessageLogs
            .Where(e => e.Id == messageLogId && e.Purpose == MessagePurpose.EmailConfirmation)
            .Select(e => e.DeliveryStatus)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<MessageLogEventRecordResult> RecordProviderEvent(string providerCorrelationId,
        string? providerMessageId, MessageLogEvent messageEvent, MessageDeliveryStatus? deliveryStatus,
        CancellationToken cancellationToken = default)
    {
        var messageLogId = await DbContext.MessageLogs
            .AsNoTracking()
            .Where(e => e.ProviderCorrelationId == providerCorrelationId)
            .Select(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (messageLogId == null) return MessageLogEventRecordResult.MessageNotFound;

        var duplicate = await DbContext.MessageLogEvents.AnyAsync(e =>
            (e.Provider == messageEvent.Provider && e.ProviderDomain == messageEvent.ProviderDomain &&
             e.ProviderEventDay == messageEvent.ProviderEventDay && e.ProviderEventId == messageEvent.ProviderEventId) ||
            (messageEvent.WebhookToken != null && e.WebhookToken == messageEvent.WebhookToken), cancellationToken);
        if (duplicate) return MessageLogEventRecordResult.Duplicate;

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            messageEvent.MessageLogId = messageLogId;
            DbContext.MessageLogEvents.Add(messageEvent);
            await SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(providerMessageId))
            {
                await DbContext.MessageLogs
                    .Where(e => e.Id == messageLogId && e.ProviderMessageId == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(e => e.ProviderMessageId, providerMessageId), cancellationToken);
            }

            if (deliveryStatus != null)
            {
                await ApplyDeliveryStatus(
                    messageLogId, deliveryStatus.Value, messageEvent.OccurredOn,
                    messageEvent.DeliveryMessage ?? messageEvent.Reason, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return MessageLogEventRecordResult.Recorded;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgresException &&
                                                   postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            DbContext.ChangeTracker.Clear();
            return MessageLogEventRecordResult.Duplicate;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task ApplyDeliveryStatus(string messageLogId, MessageDeliveryStatus incomingStatus,
        DateTime incomingStatusOn, string? statusMessage, CancellationToken cancellationToken)
    {
        while (true)
        {
            var current = await DbContext.MessageLogs
                .AsNoTracking()
                .Where(e => e.Id == messageLogId)
                .Select(e => new { e.DeliveryStatus, e.DeliveryStatusOn })
                .SingleAsync(cancellationToken);

            if (!MessageDeliveryStatusPolicy.ShouldApply(
                    current.DeliveryStatus, current.DeliveryStatusOn, incomingStatus, incomingStatusOn))
            {
                return;
            }

            var updated = await DbContext.MessageLogs
                .Where(e => e.Id == messageLogId &&
                            e.DeliveryStatus == current.DeliveryStatus &&
                            e.DeliveryStatusOn == current.DeliveryStatusOn)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.DeliveryStatus, incomingStatus)
                    .SetProperty(e => e.DeliveryStatusOn, incomingStatusOn)
                    .SetProperty(e => e.DeliveryStatusMessage, statusMessage), cancellationToken);

            if (updated == 1) return;
        }
    }

}
