using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Map.Controllers;

[Authorize]
[Route("api/map")]
public class MapController : PlanarianControllerBase<MapService>
{
    public MapController(RequestUser requestUser, TokenService tokenService, MapService service) : base(requestUser,
        tokenService, service)
    {
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetMapData([FromQuery] double north, [FromQuery] double south,
        [FromQuery] double east,
        [FromQuery] double west, [FromQuery] int zoom, CancellationToken cancellationToken)
    {
        var data = await Service.GetMapData(north, south, east, west, zoom, cancellationToken);
        return Ok(data);
    }
    
    [HttpGet("center")]
    public async Task<ActionResult<object>> GetMapCenter()
    {
        CoordinateDto data = await Service.GetMapCenter();
        return Ok(data);
    }
}