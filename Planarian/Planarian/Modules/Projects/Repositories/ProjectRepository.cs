using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Shared;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Projects.Repositories;

public class ProjectRepository : RepositoryBase
{
    public ProjectRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    #region Project Member

    public async Task<Member?> GetProjectMember(string projectId, string userId)
    {
        return await DbContext.Members.FirstOrDefaultAsync(e =>
            e.UserId == userId && e.ProjectId == projectId &&
            e.Project!.Members.Any(member => member.UserId == RequestUser.Id));
    }

    #endregion

    public async Task<PagedResult<ProjectVm>> GetProjects(FilterQuery query)
    {
        return await DbContext.Projects.Where(e => e.Members.Any(ee => ee.UserId == RequestUser.Id))
            .Select(e =>
                new ProjectVm
                {
                    Id = e.Id,
                    Name = e.Name,
                    NumberOfProjectMembers = e.Members.Count,
                    NumberOfTrips = e.Trips.Count,
                    CreatedOn = e.CreatedOn,
                    ModifiedOn = e.ModifiedOn
                }
            )
            // .QueryFilter(query.Conditions)
            .ApplyPagingAsync(query.PageNumber, query.PageSize, e => e.ModifiedOn);
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetProjectMembers(string projectId)
    {
        return await DbContext.Projects
            .Where(e => e.Id == projectId && e.Members.Any(e => e.UserId == RequestUser.Id))
            .SelectMany(e => e.Members)
            .Select(e => new SelectListItem<string>(e.User.FullName, e.UserId))
            .ToListAsync();
    }

    #region Trip

    public async Task<int> GetNumberOfTrips(string projectId)
    {
        return await DbContext.Trips.CountAsync(e =>
            e.ProjectId == projectId && e.Project.Members.Any(member => member.UserId == RequestUser.Id));
    }

    #endregion

    #region Project

    public async Task<ProjectVm?> GetProjectVm(string projectId)
    {
        var query = DbContext.Projects.Where(e =>
            e.Id == projectId && e.Members.Any(member => member.UserId == RequestUser.Id));

        return await ToProjectVm(query).FirstOrDefaultAsync();
    }


    public async Task<Project?> GetProject(string projectId)
    {
        return await DbContext.Projects.FirstOrDefaultAsync(e =>
            e.Id == projectId && e.Members.Any(member => member.UserId == RequestUser.Id));
    }

    private static IQueryable<ProjectVm> ToProjectVm(IQueryable<Project> query)
    {
        return query.Select(e => new ProjectVm(e, e.Members.Count, e.Trips.Count, e.CreatedOn, e.ModifiedOn));
    }

    #endregion
}