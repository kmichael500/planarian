using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.PlanarianSettings.Models;
using Planarian.Modules.PlanarianSettings.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.PlanarianSettings.Controllers;

[Route("api/planarian-settings")]
[Authorize(Policy = PermissionPolicyKey.PlanarianAdmin)]
public class PlanarianSettingsController : PlanarianControllerBase<PlanarianSettingsService>
{
    public PlanarianSettingsController(RequestUser requestUser, TokenService tokenService,
        PlanarianSettingsService service) : base(requestUser, tokenService, service)
    {
    }

    [HttpPost("accounts")]
    public async Task<ActionResult<string>> CreateAccount([FromBody] CreateAccountVm account,
        CancellationToken cancellationToken)
    {
        var result = await Service.CreateAccount(account, cancellationToken);

        return Ok(result);
    }
}
