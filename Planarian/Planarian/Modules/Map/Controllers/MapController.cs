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

    [HttpGet("lineplots/ids")]
    public async Task<IActionResult> GetLinePlotIds(
        [FromQuery] double north,
        [FromQuery] double south,
        [FromQuery] double east,
        [FromQuery] double west,
        [FromQuery] double zoom,
        CancellationToken cancellationToken)
    {
        var ids = await Service.GetLinePlotIds(
            north, south, east, west, zoom, cancellationToken);
        return Ok(ids);
    }

    [HttpGet("lineplots/{plotId}")]
    public async Task<IActionResult> GetLinePlotById(
        [FromRoute] string plotId,
        CancellationToken cancellationToken)
    {
        var element = await Service.GetLinePlotGeoJson(
            plotId, cancellationToken);
        if (element == null)
            return NotFound();

        Response.Headers["Cache-Control"] = "public, max-age=2678400"; // cache for 31 days
        return new JsonResult(element.Value);
    }
    
    [HttpGet("geologic-maps")]
    public async Task<ActionResult<object>> GetMapCenter(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        CancellationToken cancellationToken)
    {
        var data = await Service.GetGeologicMaps(latitude, longitude, cancellationToken);
        Response.Headers["Cache-Control"] = "public, max-age=2592000"; // cache for 30 days
        return new JsonResult(data);    }
}