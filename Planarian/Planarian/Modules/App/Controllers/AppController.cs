using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.App.Services;
using Planarian.Modules.Authentication.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.App.Controllers;

[Route("api/app")]
public class AppController : PlanarianControllerBase<AppService>
{
    private readonly IAntiforgery _antiforgery;

    public AppController(
        RequestUser requestUser,
        TokenService tokenService,
        AppService service,
        IAntiforgery antiforgery) : base(requestUser, tokenService, service)
    {
        _antiforgery = antiforgery;
    }

    [AllowAnonymous]
    [HttpGet("initialize")]
    public async Task<ActionResult<string>> Initialize()
    {
        var antiforgeryTokens = _antiforgery.GetAndStoreTokens(HttpContext);

        var request = HttpContext.Request;
        var serverBaseUrl = $"{request.Scheme}://{request.Host}";

        var result = await Service.Initialize(serverBaseUrl);
        result.AntiforgeryRequestToken = antiforgeryTokens.RequestToken;
        return new JsonResult(result);
    }

    [HttpGet("permissions/caves")]
    [Authorize]
    public async Task<ActionResult<string>> HasCavePermission([FromQuery] string? caveId, [FromQuery] string? countyId,
        [FromQuery] string permissionKey, [FromQuery] string? stateId)
    {
        var result = await Service.HasCavePermission(permissionKey, caveId, countyId, stateId);
        return new JsonResult(result);
    }
}
