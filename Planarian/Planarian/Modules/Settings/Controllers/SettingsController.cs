using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
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

    #region Objective Types

    [HttpGet("objectiveTypes")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetObjectiveTypes()
    {
        var objectiveTypes = await Service.GetObjectiveTypes();

        return new JsonResult(objectiveTypes);
    }

    [HttpGet("objectiveTypes/{objectiveTypeId:length(10)}")]
    public async Task<ActionResult<string>> GetObjectiveTypeName(string objectiveTypeId)
    {
        var objectiveType = await Service.GetObjectiveTypeName(objectiveTypeId);

        return new JsonResult(objectiveType);
    }

    #endregion

    #region Users

    [HttpGet("users/{userId:length(10)}")]
    public async Task<ActionResult<NameProfilePhotoVm>> GetUsersName(string userId)
    {
        var objectiveType = await Service.GetUsersName(userId);

        return new JsonResult(objectiveType);
    }


    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetUsers()
    {
        var users = await Service.GetUsers();

        return new JsonResult(users);
    }

    #endregion
}