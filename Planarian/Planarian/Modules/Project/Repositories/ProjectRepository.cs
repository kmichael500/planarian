using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Project.Repositories;

public class ProjectRepository : RepositoryBase
{
    public ProjectRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    #region Project Member

    public async Task<ProjectMember?> GetProjectMember(string projectId, string userId)
    {
        return await DbContext.ProjectMembers.FirstOrDefaultAsync(e => e.UserId == userId && e.ProjectId == projectId);
    }

    #endregion

    public async Task<IEnumerable<ProjectVm>> GetProjects()
    {
        return await DbContext.Projects.Where(e => e.ProjectMembers.Any(ee => ee.UserId == RequestUser.Id))
            .Select(e => new ProjectVm(e, e.ProjectMembers.Count, e.Trips.Count)).ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetProjectMembers(string projectId)
    {
        return await DbContext.Projects
            .Where(e => e.Id == projectId && e.ProjectMembers.Any(e => e.UserId == RequestUser.Id))
            .SelectMany(e => e.ProjectMembers)
            .Select(e => new SelectListItem<string>(e.User.FullName, e.UserId))
            .ToListAsync();
    }

    #region Trip

    public async Task<int> GetNumberOfTrips(string projectId)
    {
        return await DbContext.Trips.Where(e => e.ProjectId == projectId).CountAsync();
    }

    #endregion

    #region Project

    public async Task<ProjectVm?> GetProjectVm(string projectId)
    {
        var query = DbContext.Projects.Where(e => e.Id == projectId);

        return await ToProjectVm(query).FirstOrDefaultAsync();
    }


    public async Task<Model.Database.Entities.Projects.Project?> GetProject(string ProjectId)
    {
        return await DbContext.Projects.Where(e => e.Id == ProjectId).FirstOrDefaultAsync();
    }

    private static IQueryable<ProjectVm> ToProjectVm(IQueryable<Model.Database.Entities.Projects.Project> query)
    {
        return query.Select(e => new ProjectVm(e, e.ProjectMembers.Count, e.Trips.Count));
    }

    #endregion
}