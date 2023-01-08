using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Project.Services;
using Planarian.Modules.Trips.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Project.Controllers;

[Route("api/projects")]
public class ProjectController : PlanarianControllerBase<ProjectService>
{
    private readonly TripService _tripService;

    public ProjectController(RequestUser requestUser, ProjectService service, TokenService tokenService,
        TripService tripService) : base(
        requestUser, tokenService, service)
    {
        _tripService = tripService;
    }

    #region Project

    [HttpGet("{projectId:length(10)}")]
    public async Task<ActionResult<ProjectVm?>> GetProject(string projectId)
    {
        var result = await Service.GetProject(projectId);

        return new JsonResult(result);
    }

    [HttpGet]
    public async Task<ActionResult<ProjectVm?>> GetProjects()
    {
        var result = await Service.GetProjects();

        return new JsonResult(result);
    }
    
    [HttpPost]
    public async Task<ActionResult<ProjectVm>> CreateProject([FromBody] CreateOrEditProject project)
    {
        var result = await Service.CreateOrUpdateProject(project);

        return new JsonResult(result);
    }

    [HttpPut]
    public async Task<ActionResult<ProjectVm>> UpdateProject([FromBody] CreateOrEditProject project)
    {
        var result = await Service.CreateOrUpdateProject(project);

        return new JsonResult(result);
    }
    [HttpDelete("{projectId:length(10)}")]
    public async Task<IActionResult> DeleteProject(string projectId)
    {
        await Service.DeleteProject(projectId);

        return new OkResult();
    }
    

    #endregion

    #region Trips

    [HttpGet("{projectId:length(10)}/trips")]
    public async Task<ActionResult<IEnumerable<TripVm>>> GetTrips(string projectId)
    {
        var trips = await _tripService.GetTripsByProjectId(projectId);

        return new JsonResult(trips);
    }

    #endregion
    
    #region Project Member

    [HttpGet("{projectId:length(10)}/members")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetProjectMembers(string projectId)
    {
        var projectMembers = await Service.GetProjectMembers(projectId);

        return new JsonResult(projectMembers);
    }

    [HttpPost("{projectId:length(10)}/members/{userId:length(10)}")]
    public async Task<ActionResult> AddProjectMember(string projectId, string userId)
    {
        await Service.AddProjectMember(projectId, userId);

        return new OkResult();
    }
    
    [HttpPost("{projectId:length(10)}/members")]
    public async Task<ActionResult> AddProjectMembers(string projectId, [FromBody] IEnumerable<string> userIds)
    {
        await Service.AddProjectMember(projectId, userIds);

        return new OkResult();
    }

    
    [HttpDelete("{projectId:length(10)}/members/{userId:length(10)}")]
    public async Task<ActionResult> DeleteProjectMember(string projectId, string userId)
    {
        await Service.DeleteProjectMember(projectId, userId);

        return new OkResult();
    }

    #endregion

    #region Invitations

    [HttpPost("{projectId:length(10)}/members/invite")]
    public async Task<ActionResult> InviteProjectMember(string projectId, [FromBody] InviteMember invitation)
    {
        await Service.InviteProjectMember(projectId, invitation);

        return new OkResult();
    }

    #endregion
    
}