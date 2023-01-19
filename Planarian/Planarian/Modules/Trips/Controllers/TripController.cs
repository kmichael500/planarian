using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.TripObjectives;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Trips.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Trips.Controllers;

[Route("api/trips")]
[Authorize]
public class TripController : PlanarianControllerBase<TripService>
{
    public TripController(RequestUser requestUser, TripService service, TokenService tokenService) : base(requestUser,
        tokenService, service)
    {
    }

    #region Objectives

    [HttpGet("{tripId:length(10)}/objectives")]
    public async Task<ActionResult<IEnumerable<TripObjectiveVm>>> GetTripObjectives(string tripId)
    {
        var result = await Service.GetTripObjectives(tripId);

        return new JsonResult(result);
    }

    #endregion

    #region Trip Members

    [HttpGet("{tripId:length(10)}/members")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> AddTripMember(string tripId)
    {
        var projectMembers = await Service.GetTripMembers(tripId);

        return new JsonResult(projectMembers);
    }

    #endregion

    #region Trip

    [HttpGet("{tripId:length(10)}")]
    public async Task<ActionResult<TripVm?>> GetTrip(string tripId)
    {
        var result = await Service.GetTrip(tripId);

        return new JsonResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<TripVm>> CreateTrip([FromBody] CreateOrEditTripVm trip)
    {
        var result = await Service.CreateOrUpdateTrip(trip);

        return new JsonResult(result);
    }

    [HttpPut]
    public async Task<ActionResult<TripVm>> UpdateTrip([FromBody] CreateOrEditTripVm trip)
    {
        var result = await Service.CreateOrUpdateTrip(trip);

        return new JsonResult(result);
    }

    [HttpPut("{tripId:length(10)}/date")]
    public async Task<ActionResult<TripVm>> UpdateDate([FromBody] DateTime date, string tripId)
    {
        await Service.UpdateTripDate(date, tripId);

        return new OkResult();
    }

    [HttpPut("{tripId:length(10)}/name")]
    public async Task<ActionResult<TripVm>> UpdateDate([FromBody] string name, string tripId)
    {
        await Service.UpdateTripName(name, tripId);

        return new OkResult();
    }

    [HttpDelete("{tripId:length(10)}")]
    public async Task<IActionResult> DeleteTrip(string tripId)
    {
        await Service.DeleteTrip(tripId);

        return new OkResult();
    }

    #endregion
}