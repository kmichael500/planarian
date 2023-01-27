using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Invitations.Models;
using Planarian.Modules.Photos.Models;
using Planarian.Modules.Trips.Models;
using Planarian.Modules.Trips.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Trips.Controllers;

[Route("api/trips")]
[Authorize]
public class TripController : PlanarianControllerBase<TripService>
{
    public TripController(RequestUser requestUser, TripService service, TokenService tokenService) :
        base(requestUser, tokenService, service)
    {
    }

    #region Invitations

    [HttpPost("{tripId:length(10)}/members/invite")]
    public async Task<ActionResult> InviteTripMember(string tripId,
        [FromBody] InviteMember invitation)
    {
        await Service.InviteTripMember(tripId, invitation);

        return new OkResult();
    }

    #endregion

    #region Trip

    [HttpGet("{tripId:length(10)}")]
    public async Task<ActionResult<TripVm?>> GetTrip(string tripId)
    {
        var result = await Service.GetTrip(tripId);

        return new JsonResult(result);
    }

    [HttpGet("{tripId:length(10)}/tags")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetTripTags(string tripId)
    {
        var result = await Service.GetTripTags(tripId);

        return new JsonResult(result);
    }

    [HttpPost("{tripId:length(10)}/tags/{tagTypeId:length(10)}")]
    public async Task<ActionResult> AddTripTag(string tripId, string tagTypeId)
    {
        await Service.AddTripTag(tagTypeId, tripId);

        return new OkResult();
    }

    [HttpDelete("{tripId:length(10)}/tags/{tagTypeId:length(10)}")]
    public async Task<ActionResult> DeleteTripTag(string tripId, string tagTypeId)
    {
        await Service.DeleteTripTag(tagTypeId, tripId);

        return new OkResult();
    }

    [HttpPost("{tripId:length(10)}/trip-report")]
    public async Task<ActionResult> AddOrUpdateTripReport(string tripId,
        [FromBody] string tripReport)
    {
        await Service.AddOrUpdateTripReport(tripId, tripReport);

        return new OkResult();
    }

    [HttpPost("{tripId:length(10)}/name")]
    public async Task<ActionResult> UpdateTripName(string tripId,
        [FromBody] string name)
    {
        await Service.UpdateTripName(tripId, name);

        return new OkResult();
    }

    [HttpPost("{tripId:length(10)}/description")]
    public async Task<ActionResult> UpdateTripDescription(string tripId,
        [FromBody] string description)
    {
        await Service.UpdateTripDescription(tripId, description);

        return new OkResult();
    }

    [IgnoreAntiforgeryToken]
    [HttpPost("{tripId:length(10)}/photos")]
    public async Task<ActionResult> UploadTripPhotos([FromForm] IEnumerable<PhotoUpload> formData,
        string tripId)
    {
        await Service.UploadTripPhotos(formData, tripId);
        return new OkResult();
    }

    [HttpGet("{tripId:length(10)}/photos")]
    public async Task<ActionResult<IEnumerable<PhotoVm>>> GetTripPhotos(string tripId)
    {
        var photos = await Service.GetTripPhotos(tripId);
        return new JsonResult(photos);
    }

    [HttpPut]
    public async Task<ActionResult<TripVm>> UpdateTrip(
        [FromBody] CreateOrEditTripVm trip)
    {
        var result = await Service.CreateOrUpdateTrip(trip);

        return new JsonResult(result);
    }

    [HttpDelete("{tripId:length(10)}")]
    public async Task<IActionResult> DeleteTrip(string tripId)
    {
        await Service.DeleteTrip(tripId);

        return new OkResult();
    }

    #endregion

    #region Trip Member

    [HttpGet("{tripId:length(10)}/members")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetTripMembers(string tripId)
    {
        var tripMembers = await Service.GetTripMembers(tripId);

        return new JsonResult(tripMembers);
    }

    [HttpPost("{tripId:length(10)}/members/{userId:length(10)}")]
    public async Task<ActionResult> AddTripMember(string tripId, string userId)
    {
        await Service.AddTripMember(tripId, userId);

        return new OkResult();
    }

    [HttpPost("{tripId:length(10)}/members")]
    public async Task<ActionResult> AddTripMembers(string tripId,
        [FromBody] IEnumerable<string> userIds)
    {
        await Service.AddTripMembers(tripId, userIds);

        return new OkResult();
    }

    [HttpDelete("{tripId:length(10)}/members/{userId:length(10)}")]
    public async Task<ActionResult> DeleteTripMember(string tripId, string userId)
    {
        await Service.DeleteTripMember(tripId, userId);

        return new OkResult();
    }

    #endregion

    #region Leads

    [HttpGet("{tripId:length(10)}/leads")]
    public async Task<ActionResult<IEnumerable<LeadVm>>> GetTripLeads(string tripId)
    {
        var leads = await Service.GetTripLeads(tripId);
        return new JsonResult(leads);
    }

    [HttpPost("{tripId:length(10)}/leads")]
    public async Task<ActionResult> AddTripLeads(string tripId, [FromBody] IEnumerable<CreateLeadVm> leads)
    {
        await Service.AddTripLeads(leads, tripId);
        return new OkResult();
    }

    #endregion
}