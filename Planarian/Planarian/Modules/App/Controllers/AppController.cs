using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.App.Services;
using Planarian.Modules.Authentication.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.App.Controllers;

[Route("api/app")]
[AllowAnonymous]
public class AppController : PlanarianControllerBase<AppService>
{
    public AppController(RequestUser requestUser, TokenService tokenService, AppService service) : base(requestUser,
        tokenService, service)
    {
    }

    [HttpGet("initialize")]
    public async Task<ActionResult<string>> Initialize()
    {
        var request = HttpContext.Request;
        var serverBaseUrl = $"{request.Scheme}://{request.Host}";

        var result = await Service.Initialize(serverBaseUrl);
        return new JsonResult(result);
    }


}