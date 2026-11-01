using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Shared.Base;

public abstract class DataAccessBase : DataAccessBase<PlanarianDbContext>
{
    protected DataAccessBase(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }
}

public abstract class DataAccessBase<TDbContext> where TDbContext : PlanarianDbContextBase
{
    protected readonly TDbContext DbContext;
    protected readonly RequestUser RequestUser;

    protected DataAccessBase(TDbContext dbContext, RequestUser requestUser)
    {
        if (!ReferenceEquals(dbContext.RequestUser, requestUser))
            throw new InvalidOperationException(
                "A data-access RequestUser must be bound to the same DbContext instance.");

        DbContext = dbContext;
        RequestUser = requestUser;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        DbContext.Database.BeginTransactionAsync(cancellationToken);

    public void Add(EntityBase entity) => DbContext.Add(entity);

    public void Delete(EntityBase entity) => DbContext.Remove(entity);

    public void DeleteRange(IEnumerable<EntityBase> entities) => DbContext.RemoveRange(entities);

    protected async Task RunInTransactionAsync(Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        if (DbContext.Database.CurrentTransaction is not null)
        {
            await work(cancellationToken);
            return;
        }

        await RunInOwnedTransactionAsync(work, cancellationToken);
    }

    protected async Task<T> RunInTransactionAsync<T>(Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        if (DbContext.Database.CurrentTransaction is not null)
            return await work(cancellationToken);

        return await RunInOwnedTransactionAsync(work, cancellationToken);
    }

    protected Task RunInOwnedTransactionAsync(Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default) =>
        RunInOwnedTransactionAsync(async token =>
        {
            await work(token);
            return true;
        }, cancellationToken);

    protected async Task<T> RunInOwnedTransactionAsync<T>(Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        if (DbContext.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("This data-access operation requires ownership of its database transaction and cannot join an existing transaction.");

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await work(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    protected async Task AddRangeBatchedAndClearAsync<TEntity>(IEnumerable<TEntity> entities, int batchSize,
        CancellationToken cancellationToken = default) where TEntity : EntityBase
    {
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (DbContext.ChangeTracker.Entries().Any())
            throw new InvalidOperationException(
                "Batched inserts that clear tracking require an empty change tracker so unrelated tracked state cannot be discarded.");

        foreach (var batch in entities.Chunk(batchSize))
        {
            DbContext.AddRange(batch);
            await SaveChangesAsync(cancellationToken);
            DbContext.ChangeTracker.Clear();
        }
    }

    protected async Task<int> DeleteBatchAsync<TEntity>(IQueryable<TEntity> query, int batchSize,
        CancellationToken cancellationToken = default) where TEntity : EntityBase
    {
        var ids = await query.AsNoTracking().Take(batchSize).Select(entity => entity.Id)
            .ToListAsync(cancellationToken);
        if (ids.Count == 0) return 0;

        // Reuse the original scoped query for the DELETE. This is important:
        // selecting tenant-scoped IDs and then deleting by IDs alone would
        // turn a safe lookup into an unscoped mutation.
        return await query.Where(entity => ids.Contains(entity.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public void SetPropertiesModified<TEntity>(TEntity entity, params Expression<Func<TEntity, object>>[] properties)
        where TEntity : class
    {
        var entry = DbContext.Entry(entity);
        foreach (var property in properties)
            entry.Property(property).IsModified = true;
    }
}
