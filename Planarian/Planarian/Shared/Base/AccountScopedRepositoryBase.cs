using Planarian.Model.Database;
using Planarian.Model.Shared;

namespace Planarian.Shared.Base;

public abstract class AccountScopedRepositoryBase : AccountScopedRepositoryBase<PlanarianDbContext>
{
    protected AccountScopedRepositoryBase(PlanarianDbContext dbContext, RequestUser requestUser)
        : base(dbContext, requestUser)
    {
    }
}

public abstract class AccountScopedRepositoryBase<TDbContext> : RepositoryBase<TDbContext>
    where TDbContext : PlanarianDbContextBase
{
    protected AccountScopedRepositoryBase(TDbContext dbContext, RequestUser requestUser)
        : base(dbContext, requestUser)
    {
        Scope = AccountExecutionScope.Require(requestUser);
    }

    protected AccountExecutionScope Scope { get; }
    protected string AccountId => Scope.AccountId;
    protected string UserId => Scope.UserId;
}
