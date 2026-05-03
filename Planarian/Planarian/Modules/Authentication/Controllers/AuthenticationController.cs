using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Services;
using Planarian.Shared.Attributes;
using Planarian.Shared.Base;

namespace Planarian.Modules.Authentication.Controllers;

[Route("api/authentication")]
public class AuthenticationController : PlanarianControllerBase<AuthenticationService>
{
    public AuthenticationController(
        RequestUser requestUser,
        TokenService tokenService,
        AuthenticationService service) :
        base(requestUser, tokenService, service)
    {
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [Throttle]
    public async Task<IActionResult> Login([FromBody] BrowserLoginVm values, CancellationToken cancellationToken)
    {
        await Service.AuthenticateEmailPassword(
            HttpContext,
            values.EmailAddress,
            values.Password,
            values.Remember ?? false);

        return Ok();
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost("token")]
    [Throttle]
    public async Task<ActionResult<ApiTokenLoginResultVm>> Token([FromBody] TokenLoginVm values, CancellationToken cancellationToken)
    {
        var accessToken = await Service.AuthenticateEmailPassword(values.EmailAddress, values.Password);
        return Ok(new ApiTokenLoginResultVm(accessToken));
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Service.Logout(HttpContext);
        return new OkResult();
    }
}
