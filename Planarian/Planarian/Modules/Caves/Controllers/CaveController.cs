using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Caves.Controllers;

[Route("api/caves")]
[Authorize]
public class CaveController : PlanarianControllerBase<CaveService>
{
    public CaveController(RequestUser requestUser, TokenService tokenService, CaveService service) : base(requestUser,
        tokenService, service)
    {
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CaveVm>>> GetTrips([FromQuery] FilterQuery query)
    {
        var trips = await Service.GetCaves(query);

        return new JsonResult(trips);
    }

  
}