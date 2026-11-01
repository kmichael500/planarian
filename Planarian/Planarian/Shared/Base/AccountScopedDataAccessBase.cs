using Planarian.Model.Database;
using Planarian.Model.Shared;

namespace Planarian.Shared.Base;

public abstract class AccountScopedDataAccessBase : AccountScopedDataAccessBase<PlanarianDbContext>
{
    protected AccountScopedDataAccessBase(PlanarianDbContext dbContext, RequestUser requestUser)
        : base(dbContext, requestUser)
    {
    }
}

public abstract class AccountScopedDataAccessBase<TDbContext> : DataAccessBase<TDbContext>
    where TDbContext : PlanarianDbContextBase
{
    protected AccountScopedDataAccessBase(TDbContext dbContext, RequestUser requestUser)
        : base(dbContext, requestUser)
    {
        Scope = AccountExecutionScope.Require(requestUser);
    }

    protected AccountExecutionScope Scope { get; }
    protected string AccountId => Scope.AccountId;
    protected string UserId => Scope.UserId;
}
