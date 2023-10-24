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
    
    [AllowAnonymous]
    [HttpGet("{z:int}/{x:int}/{y:int}.mvt")]
    public async Task<IActionResult> GetTile(int z, int x, int y)
    {
        var mvtData = await Service.GetEntrancesMVTAsync(z, x, y);
        if (mvtData == null)
        {
            return NotFound();
        }

        return File(mvtData, "application/vnd.mapbox-vector-tile");
    }
    
    [AllowAnonymous]
    [HttpGet("style")]
    public IActionResult GetStyle()
    {
        var styleSpecification = new
        {
            version = 8,
            name = "Custom Style",
            sources = new Dictionary<string, object>
            {
                ["entrances"] = new
                {
                    type = "vector",
                    tiles = new string[] { "https://localhost:7111/api/map/{z}/{x}/{y}.mvt" }
                }
            },
            layers = new object[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = "entrances",
                    ["type"] = "circle",
                    ["source"] = "entrances",
                    ["source-layer"] = "entrances",
                    ["paint"] = new Dictionary<string, object>
                    {
                        ["circle-radius"] = 5,
                        ["circle-color"] = "#ff0000"
                    }
                }
            }
        };

        return Ok(styleSpecification);
    }

}