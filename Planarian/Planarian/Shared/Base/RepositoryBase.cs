using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Shared.Base;

public abstract class RepositoryBase : RepositoryBase<PlanarianDbContext>
{
    protected RepositoryBase(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }
}

public abstract class RepositoryBase<TDbContext> where TDbContext : PlanarianDbContextBase
{
    protected readonly TDbContext DbContext;
    protected readonly RequestUser RequestUser;

    protected RepositoryBase(TDbContext dbContext, RequestUser requestUser)
    {
        DbContext = dbContext;
        RequestUser = requestUser;
        DbContext.RequestUser = requestUser;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await DbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return await DbContext.Database.BeginTransactionAsync(cancellationToken);
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

    public void Add(EntityBase entity)
    {
        DbContext.Add(entity);
    }

    public void Delete(EntityBase entity)
    {
        DbContext.Remove(entity);
    }

    public void DeleteRange(IEnumerable<EntityBase> entities)
    {
        DbContext.RemoveRange(entities);
    }

    public void SetPropertiesModified<TEntity>(TEntity entity, params Expression<Func<TEntity, object>>[] properties)
        where TEntity : class
    {
        var entry = DbContext.Entry(entity);
        foreach (var property in properties)
        {
            entry.Property(property).IsModified = true;
        }
    }
}
