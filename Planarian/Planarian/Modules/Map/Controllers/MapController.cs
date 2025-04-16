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

    [HttpGet("lineplots")]
    public async Task<IActionResult> GetLinePlots(
        [FromQuery] double north, 
        [FromQuery] double south,
        [FromQuery] double east,
        [FromQuery] double west, 
        [FromQuery] double zoom, 
        CancellationToken cancellationToken)
    {
        var data = await Service.GetLinePlots(north, south, east, west, zoom, cancellationToken);
     
        return new JsonResult(data);
    }



    [HttpGet("center")]
    public async Task<ActionResult<object>> GetMapCenter()
    {
        CoordinateDto data = await Service.GetMapCenter();
        return Ok(data);
    }
    
    [HttpGet("{z:int}/{x:int}/{y:int}.mvt")]
    public async Task<IActionResult> GetTile(int z, int x, int y, CancellationToken cancellationToken)
    {
        var mvtData = await Service.GetEntrancesMVTAsync(z, x, y, cancellationToken);
        // Response.Headers.Add("Cache-Control", "public, max-age=86400"); // cache for 1 day
        if (mvtData == null)
        {
            return NotFound();
        }

        return File(mvtData, "application/vnd.mapbox-vector-tile");
    }
    
}