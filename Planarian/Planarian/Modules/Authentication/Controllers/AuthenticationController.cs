using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Library.Extensions.String;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Authentication.Controllers;

[Route("api/authentication")]
public class AuthenticationController : PlanarianControllerBase<AuthenticationService>
{
    private readonly UserService _userService;

    public AuthenticationController(RequestUser requestUser, TokenService tokenService, AuthenticationService service, UserService userService) :
        base(requestUser, tokenService, service)
    {
        _userService = userService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<string>> Login([FromBody] UserLoginVm values, CancellationToken cancellationToken)
    {
        var token = await Service.AuthenticateEmailPassword(values.EmailAddress, values.Password);
        if (!values.InvitationCode.IsNullOrWhiteSpace())
        {
            await _userService.ClaimInvitation(values.InvitationCode, cancellationToken);
        }

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