using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Shared;
using Planarian.Modules.Invitations.Models;
using Planarian.Modules.Projects.Repositories;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Base;

namespace Planarian.Modules.Projects.Services;

public class ProjectService : ServiceBase<ProjectRepository>
{
    private readonly UserRepository _userRepository;

    public ProjectService(ProjectRepository repository, RequestUser requestUser, UserRepository userRepository) : base(
        repository, requestUser)
    {
        _userRepository = userRepository;
    }

    public async Task<IEnumerable<ProjectVm>> GetProjects()
    {
        return await Repository.GetProjects();
    }

    #region Invitations

    public async Task InviteProjectMember(string projectId, InviteMember invitation)
    {
        var project = await Repository.GetProject(projectId);

        if (project == null) throw new NullReferenceException("Project not found");

        var user = await _userRepository.GetUserByEmail(invitation.Email);
        if (user != null)
        {
            await AddProjectMember(projectId, user.Id);
        }
        else
        {
            var entity = new User(invitation.FirstName, invitation.LastName, invitation.Email);
            _userRepository.Add(entity);
            await Repository.SaveChangesAsync();
            await AddProjectMember(projectId, entity.Id);
        }
    }

    #endregion

    #region Project

    public async Task<ProjectVm> CreateOrUpdateProject(CreateOrEditProject values)
    {
        var isNew = string.IsNullOrWhiteSpace(values.Id);
        var project = values.Id != null
            ? await Repository.GetProject(values.Id) ?? new Model.Database.Entities.Projects.Project()
            : new Model.Database.Entities.Projects.Project();

        project.Name = values.Name;
        if (isNew)
        {
            Repository.Add(project);
            await Repository.SaveChangesAsync();
            await AddProjectMember(project.Id, RequestUser.Id, false);
        }

        await Repository.SaveChangesAsync();

        var numberOfTrips = await Repository.GetNumberOfTrips(project.Id);

        return new ProjectVm(project, 1, numberOfTrips);
    }

    public async Task<ProjectVm?> GetProject(string projectId)
    {
        return await Repository.GetProjectVm(projectId);
    }

    public async Task DeleteProject(string projectId)
    {
        var project = await Repository.GetProject(projectId);
        if (project != null)
        {
            Repository.Delete(project);
            await Repository.SaveChangesAsync();
        }
    }

    #endregion

    #region Project Member

    public async Task<IEnumerable<SelectListItem<string>>> GetProjectMembers(string projectId)
    {
        return await Repository.GetProjectMembers(projectId);
    }

    public async Task AddProjectMember(string projectId, string userId, bool saveChanges = true)
    {
        var project = await Repository.GetProject(projectId);
        if (project == null) throw new NullReferenceException("Project not found");
        var projectMember = new Member
        {
            UserId = userId,
            ProjectId = projectId
        };
        Repository.Add(projectMember);

        if (saveChanges) await Repository.SaveChangesAsync();
    }

    public async Task AddProjectMember(string projectId, IEnumerable<string> userIds, bool saveChanges = true)
    {
        foreach (var userId in userIds) await AddProjectMember(projectId, userId, false);

        if (saveChanges) await Repository.SaveChangesAsync();
    }

    public async Task DeleteProjectMember(string projectId, string userId)
    {
        var projectMember = await Repository.GetProjectMember(projectId, userId);
        if (projectMember != null)
        {
            Repository.Delete(projectMember);
            await Repository.SaveChangesAsync();
        }
    }

    #endregion
}