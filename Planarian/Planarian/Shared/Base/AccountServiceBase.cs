using Planarian.Model.Shared;

namespace Planarian.Shared.Base;

public abstract class AccountServiceBase : ServiceBase
{
    protected AccountServiceBase(RequestUser requestUser) : base(requestUser)
    {
        Scope = AccountExecutionScope.Require(requestUser);
    }

    protected AccountExecutionScope Scope { get; }
    protected string AccountId => Scope.AccountId;
    protected string UserId => Scope.UserId;
}
