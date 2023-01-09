using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Authentication.Controllers;

[Route("api/authentication")]
public class AuthenticationController : PlanarianControllerBase<AuthenticationService>
{
    public AuthenticationController(RequestUser requestUser, TokenService tokenService, AuthenticationService service) :
        base(requestUser, tokenService, service)
    {
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<string>> Login([FromBody] UserLoginVm values)
    {
        var token = await Service.AuthenticateEmailPassword(values.EmailAddress, values.Password);
        return new JsonResult(token);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await Service.Logout(HttpContext);
        return new OkResult();
    }
}