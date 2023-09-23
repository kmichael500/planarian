using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.App.Repositories;

public class AppRepository : RepositoryBase
{
    public AppRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext,
        requestUser)
    {
    }
}