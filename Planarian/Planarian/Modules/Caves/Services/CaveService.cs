using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Caves.Controllers;

public class CaveService : ServiceBase<CaveRepository>
{
    public CaveService(CaveRepository repository, RequestUser requestUser) : base(repository, requestUser)
    {
    }

    public async Task<PagedResult<CaveVm>> GetCaves(FilterQuery query)
    {
        return await Repository.GetCaves(query);
    }
}