using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Shared.Base;

public abstract class RepositoryBase
{
    protected readonly PlanarianDbContext DbContext;
    protected readonly RequestUser RequestUser;

    protected RepositoryBase(PlanarianDbContext dbContext, RequestUser requestUser)
    {
        DbContext = dbContext;
        RequestUser = requestUser;
        DbContext.RequestUser = requestUser;
    }

    public async Task SaveChangesAsync()
    {
        var result = await DbContext.SaveChangesAsync();
    }
    
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return await DbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task BulkInsertAsync(IEnumerable<EntityBase> entities, BulkConfig? bulkConfig = null,
        Action<int, int>? onBatchProcessed = null,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
    {
        entities = entities.ToList();
        var totalEntities = entities.Count();
        
        var processed = 0;
        foreach (var batch in entities.Chunk(batchSize))
        {
            await DbContext.BulkInsertAsync(batch, bulkConfig, cancellationToken: cancellationToken);
            processed += batch.Length;
            onBatchProcessed?.Invoke(processed, totalEntities);
        }
    }

    public async Task<int> ExecuteRawSql(string sql, CancellationToken cancellationToken = default)
    {
        return await DbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken:cancellationToken);
    }

    public async Task BulkSaveChangesAsync()
    {
        await DbContext.BulkSaveChangesAsync();
    }

    public void Add(EntityBase entity)
    {
        DbContext.Add(entity);
    }

    public void Delete(EntityBase entity)
    {
        DbContext.Remove(entity);
    }
}