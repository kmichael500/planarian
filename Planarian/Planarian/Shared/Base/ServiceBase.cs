using Planarian.Model.Shared;

namespace Planarian.Shared.Base;

public abstract class ServiceBase
{
    protected readonly RequestUser RequestUser;

    protected ServiceBase(RequestUser requestUser)
    {
        RequestUser = requestUser;
    }
}

public abstract class ServiceBase<TRepository> : ServiceBase
{
    protected readonly TRepository Repository;

    protected ServiceBase(TRepository repository, RequestUser requestUser) : base(requestUser)
    {
        Repository = repository;
    }
}