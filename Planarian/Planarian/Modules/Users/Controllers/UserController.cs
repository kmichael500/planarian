using Microsoft.AspNetCore.Authorization;
using Planarian.Shared.Base;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Models;
using Planarian.Modules.Users.Services;

namespace Planarian.Modules.Users.Controllers;

[Route("api/users")]
[Authorize]
public class UserController : PlanarianControllerBase<UserService>
{
    public UserController(RequestUser requestUser, UserService service, TokenService tokenService) : base(requestUser, tokenService, service)
    {
    }

    #region Users
    [HttpGet("current")]
    public async Task<ActionResult<UserVm>> GetCurrentUser()
    {
        var user = await Service.GetUserVm(RequestUser.Id);
        return new JsonResult(user);
    }

    [HttpPut("current")]
    public async Task<ActionResult> UpdateCurrentUser([FromBody] UserVm user)
    {
        await Service.UpdateCurrentUser(user);

        return new OkResult();
    }
    
    [HttpPut("current/password")]
    public async Task<ActionResult> UpdateCurrentUserPassword([FromBody] string password)
    {
        await Service.UpdateCurrentUserPassword(password);

        return new OkResult();
    }

    #endregion
}