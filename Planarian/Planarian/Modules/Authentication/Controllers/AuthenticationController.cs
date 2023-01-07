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
    public AuthenticationController(RequestUser requestUser, AuthenticationService service) : base(requestUser, service)
    {
    }
    
    [HttpPost("login")]
    public async Task<ActionResult<string>> Login([FromBody] UserLoginVm values)
    {
        var token = await Service.AuthenticateEmailPassword(values.EmailAddress, values.Password, HttpContext);
        return new JsonResult(token);
    }
    
    [HttpPost("register")]
    public async Task<ActionResult<string>> Register([FromBody] RegisterUserVm values)
    {
        throw new NotImplementedException();
    }
    
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await Service.Logout(HttpContext);
        return new OkResult();
    }
}