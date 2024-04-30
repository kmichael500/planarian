using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Settings.Models;
using Planarian.Modules.Settings.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Settings.Controllers;

[Route("api/settings")]
public class SettingsController : PlanarianControllerBase<SettingsService>
{
    public SettingsController(RequestUser requestUser, SettingsService service, TokenService tokenService) : base(
        requestUser, tokenService, service)
    {
    }

    #region Tags

    [HttpGet("tags/states/{stateId:length(10)}/counties")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetStateCounties(string stateId)
    {
        var counties = await Service.GetStateCounties(stateId);

        return new JsonResult(counties);
    }

    [HttpGet("tags/counties/{countyId:length(10)}")]
    public async Task<ActionResult<string?>> GetCountyName(string countyId)
    {
        var countyName = await Service.GetCountyName(countyId);

        return new JsonResult(countyName);
    }

    [HttpGet("tags/states/")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetStates()
    {
        var states = await Service.GetStates();

        return new JsonResult(states);
    }

    [HttpGet("tags/states/{stateId:length(10)}")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetStateName(string stateId)
    {
        var stateName = await Service.GetStateName(stateId);

        return new JsonResult(stateName);
    }

    [HttpGet("tags")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetGeologyTags(string key,
        string? projectId = null)
    {
        var geologyTags = await Service.GetTags(key);

        return new JsonResult(geologyTags);
    }

    [HttpGet("tags/{tagTypeId:length(10)}")]
    public async Task<ActionResult<string>> GetTagTypeName(string tagTypeId)
    {
        var name = await Service.GetTagTypeName(tagTypeId);

        return new JsonResult(name);
    }

    #endregion

    #region Users

    [HttpGet("users/{userId:length(10)}")]
    public async Task<ActionResult<NameProfilePhotoVm>> GetUsersName(string userId)
    {
        var userNameProfilePhoto = await Service.GetUsersName(userId);

        return new JsonResult(userNameProfilePhoto);
    }


    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetUsers()
    {
        var users = await Service.GetUsers();

        return new JsonResult(users);
    }

    #endregion
}