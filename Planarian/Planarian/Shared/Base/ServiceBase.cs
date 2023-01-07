using Planarian.Model.Shared;

namespace Planarian.Shared.Base;

public abstract class ServiceBase
{
    protected ServiceBase()
    {
    }
}

public abstract class ServiceBase<TRepository> : ServiceBase where TRepository : RepositoryBase
{
    protected readonly TRepository Repository;
    protected readonly RequestUser RequestUser;

    protected ServiceBase(TRepository repository, RequestUser requestUser)
    {
        Repository = repository;
        RequestUser = requestUser;
    }
}