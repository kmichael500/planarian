using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.TripObjectives;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Leads.Models;
using Planarian.Modules.Project.Controllers;
using Planarian.Modules.TripObjectives.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.TripObjectives.Controllers;

[Route("api/tripObjectives")]
[Authorize]
public class TripObjectiveController : PlanarianControllerBase<TripObjectiveService>
{
    public TripObjectiveController(RequestUser requestUser, TripObjectiveService service, TokenService tokenService) :
        base(requestUser, tokenService, service)
    {
    }

    #region Invitations

    [HttpPost("{tripObjectiveId:length(10)}/members/invite")]
    public async Task<ActionResult> InviteTripObjectiveMember(string tripObjectiveId,
        [FromBody] InviteMember invitation)
    {
        await Service.InviteTripObjectiveMember(tripObjectiveId, invitation);

        return new OkResult();
    }

    #endregion

    #region Trip Objective

    [HttpGet("{tripObjectiveId:length(10)}")]
    public async Task<ActionResult<TripObjectiveVm?>> GetTripObjective(string tripObjectiveId)
    {
        var result = await Service.GetTripObjective(tripObjectiveId);

        return new JsonResult(result);
    }

    [HttpGet("{tripObjectiveId:length(10)}/tags")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetTripObjectiveTags(string tripObjectiveId)
    {
        var result = await Service.GetTripObjectiveTags(tripObjectiveId);

        return new JsonResult(result);
    }

    [HttpPost("{tripObjectiveId:length(10)}/tags/{tagId:length(10)}")]
    public async Task<ActionResult> AddTripObjectiveTag(string tripObjectiveId, string tagId)
    {
        await Service.AddTagType(tagId, tripObjectiveId);

        return new OkResult();
    }

    [HttpDelete("{tripObjectiveId:length(10)}/tags/{tagId:length(10)}")]
    public async Task<ActionResult> DeleteTripObjectiveTag(string tripObjectiveId, string tagId)
    {
        await Service.DeleteTripObjectiveTag(tagId, tripObjectiveId);

        return new OkResult();
    }

    // TODO Move this to the trip controller
    [HttpPost]
    public async Task<ActionResult<TripObjectiveVm>> CreateTripObjective(
        [FromBody] CreateOrEditTripObjectiveVm tripObjective)
    {
        var result = await Service.CreateOrUpdateTripObjective(tripObjective);

        return new JsonResult(result);
    }

    [HttpPost("{tripObjectiveId:length(10)}/tripReport")]
    public async Task<ActionResult> CreateTripObjective(string tripObjectiveId,
        [FromBody] string tripReport)
    {
        await Service.AddOrUpdateTripReport(tripObjectiveId, tripReport);

        return new OkResult();
    }

    [HttpPost("{tripObjectiveId:length(10)}/name")]
    public async Task<ActionResult> UpdateObjectiveName(string tripObjectiveId,
        [FromBody] string name)
    {
        await Service.UpdateObjectiveName(tripObjectiveId, name);

        return new OkResult();
    }

    [HttpPost("{tripObjectiveId:length(10)}/description")]
    public async Task<ActionResult> UpdateObjectiveDescription(string tripObjectiveId,
        [FromBody] string description)
    {
        await Service.UpdateObjectiveDescription(tripObjectiveId, description);

        return new OkResult();
    }

    [IgnoreAntiforgeryToken]
    [HttpPost("{tripObjectiveId:length(10)}/photos")]
    public async Task<ActionResult> UploadPhotos([FromForm] IEnumerable<TripUploadPhoto> formData,
        string tripObjectiveId)
    {
        await Service.UploadPhotos(formData, tripObjectiveId);
        return new OkResult();
    }

    [HttpGet("{tripObjectiveId:length(10)}/photos")]
    public async Task<ActionResult<IEnumerable<TripPhotoVm>>> GetPhotos(string tripObjectiveId)
    {
        var photos = await Service.GetPhotos(tripObjectiveId);
        return new JsonResult(photos);
    }

    [HttpPut]
    public async Task<ActionResult<TripObjectiveVm>> UpdateTripObjective(
        [FromBody] CreateOrEditTripObjectiveVm tripObjective)
    {
        var result = await Service.CreateOrUpdateTripObjective(tripObjective);

        return new JsonResult(result);
    }

    [HttpDelete("{tripObjectiveId:length(10)}")]
    public async Task<IActionResult> DeleteTripObjective(string tripObjectiveId)
    {
        await Service.DeleteTripObjective(tripObjectiveId);

        return new OkResult();
    }

    #endregion

    #region Trip Objective Member

    [HttpGet("{tripObjectiveId:length(10)}/members")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetTripObjectiveMembers(string tripObjectiveId)
    {
        var tripObjectiveMembers = await Service.GetTripObjectiveMembers(tripObjectiveId);

        return new JsonResult(tripObjectiveMembers);
    }

    [HttpPost("{tripObjectiveId:length(10)}/members/{userId:length(10)}")]
    public async Task<ActionResult> AddTripObjectiveMember(string tripObjectiveId, string userId)
    {
        await Service.AddTripObjectiveMember(tripObjectiveId, userId);

        return new OkResult();
    }

    [HttpPost("{tripObjectiveId:length(10)}/members")]
    public async Task<ActionResult> AddTripObjectiveMembers(string tripObjectiveId,
        [FromBody] IEnumerable<string> userIds)
    {
        await Service.AddTripObjectiveMember(tripObjectiveId, userIds);

        return new OkResult();
    }

    [HttpDelete("{tripObjectiveId:length(10)}/members/{userId:length(10)}")]
    public async Task<ActionResult> DeleteTripObjectiveMember(string tripObjectiveId, string userId)
    {
        await Service.DeleteTripObjectiveMember(tripObjectiveId, userId);

        return new OkResult();
    }

    #endregion

    #region Leads

    [HttpGet("{tripObjectiveId:length(10)}/leads")]
    public async Task<ActionResult<IEnumerable<LeadVm>>> GetLeads(string tripObjectiveId)
    {
        var leads = await Service.GetLeads(tripObjectiveId);
        return new JsonResult(leads);
    }

    [HttpPost("{tripObjectiveId:length(10)}/leads")]
    public async Task<ActionResult> AddLeads(string tripObjectiveId, [FromBody] IEnumerable<CreateLeadVm> leads)
    {
        await Service.AddLeads(leads, tripObjectiveId);
        return new OkResult();
    }

    #endregion
}

public class TripPhotoVm
{
    public TripPhotoVm(string id, string? title, string? description, string url)
    {
        Id = id;
        Title = title;
        Description = description;
        Url = url;
    }

    public TripPhotoVm()
    {
    }

    public string Id { get; set; } = null!;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string Url { get; set; }
}