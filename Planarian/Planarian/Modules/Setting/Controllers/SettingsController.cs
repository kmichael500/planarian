using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Settings.Models;
using Planarian.Modules.Settings.Services;
using Planarian.Modules.Users.Models;
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

    [HttpGet("tags/trip")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetTripTags()
    {
        var tripTags = await Service.GetTripTags();

        return new JsonResult(tripTags);
    }

    [HttpGet("tags/{tagId:length(10)}")]
    public async Task<ActionResult<string>> GetTagName(string tagId)
    {
        var tag = await Service.GetTagName(tagId);

        return new JsonResult(tag);
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