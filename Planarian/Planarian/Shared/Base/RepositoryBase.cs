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

    public void AddRange(IEnumerable<EntityBase> entities) => DbContext.AddRange(entities);

    protected async Task<int> DeleteBatchAsync<TEntity>(IQueryable<TEntity> query, int batchSize,
        CancellationToken cancellationToken = default) where TEntity : EntityBase
    {
        var ids = await query.AsNoTracking().Take(batchSize).Select(entity => entity.Id)
            .ToListAsync(cancellationToken);
        if (ids.Count == 0) return 0;

        return await DbContext.Set<TEntity>().IgnoreQueryFilters()
            .Where(entity => ids.Contains(entity.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }


    public async Task<int> ExecuteRawSql(string sql, CancellationToken cancellationToken = default)
    {
        return await DbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
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

    protected string GetTableName<TEntity>()
    {
        var tableName = DbContext.Model.FindEntityType(typeof(TEntity))?.GetTableName();

        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw ApiExceptionDictionary.NotFound(nameof(tableName));
        }

        return tableName.Quote();
    }

    protected string GetColumnName<TEntity>(Expression<Func<TEntity, object?>> propertyExpression)
    {
        // Extract the property name from the expression
        var memberExpression = propertyExpression.Body as MemberExpression ??
                               (propertyExpression.Body as UnaryExpression)?.Operand as MemberExpression;
        if (memberExpression == null)
        {
            throw new ArgumentException("Invalid property expression", nameof(propertyExpression));
        }

        var propertyName = memberExpression.Member.Name;

        var columnName = DbContext.Model.FindEntityType(typeof(TEntity))?.FindProperty(propertyName)?.GetColumnName();

        if (string.IsNullOrWhiteSpace(columnName))
        {
            throw ApiExceptionDictionary.NotFound(nameof(columnName));
        }

        return columnName.Quote();
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
