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
        await DbContext.SaveChangesAsync();
    }
    
    public async Task<IDbContextTransaction> BeginTransactionAsync()
    {
        return await DbContext.Database.BeginTransactionAsync();
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