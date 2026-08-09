using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;

namespace Planarian.Shared.Repositories;

public sealed class ThrottleEventLogRepository
{
    private readonly IDbContextFactory<PlanarianDbContext> _dbContextFactory;

    public ThrottleEventLogRepository(IDbContextFactory<PlanarianDbContext> dbContextFactory) =>
        _dbContextFactory = dbContextFactory;

    public async Task AddAsync(ThrottleEventLog entry, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.ThrottleEventLogs.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteBeforeAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.ThrottleEventLogs.Where(entry => entry.OccurredOn < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
