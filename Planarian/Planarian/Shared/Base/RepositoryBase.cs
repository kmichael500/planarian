using Planarian.Model.Database;
using Planarian.Model.Shared;

namespace Planarian.Shared.Base;

/// <summary>
/// Semantic base for resource/aggregate-style repositories. Shared EF/unit-of-work mechanics live in DataAccessBase.
/// </summary>
public abstract class RepositoryBase : RepositoryBase<PlanarianDbContext>
{
    protected RepositoryBase(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }
}

public abstract class RepositoryBase<TDbContext> : DataAccessBase<TDbContext>
    where TDbContext : PlanarianDbContextBase
{
    protected RepositoryBase(TDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }
}
