using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Map.Models;
using Planarian.Modules.Map.Services;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Attributes;
using Planarian.Shared.Base;

namespace Planarian.Modules.Map.Controllers;

[Authorize]
[Route("api/map")]
public class MapController : PlanarianControllerBase<MapService>
{
    private static readonly TimeSpan MaximumStreamGageObservationRange = TimeSpan.FromDays(90);

    public MapController(RequestUser requestUser, TokenService tokenService, MapService service) : base(requestUser,
        tokenService, service)
    {
    }


    [HttpGet]
    [Throttle(RequestsPerMinute = 600)]
    public async Task<ActionResult<IEnumerable<object>>> GetMapData(
        [FromQuery] double north,
        [FromQuery] double south,
        [FromQuery] double east,
        [FromQuery] double west,
        [FromQuery] int zoom,
        CancellationToken cancellationToken)
    {
        return Ok(await Service.GetMapData(north, south, east, west, zoom, cancellationToken));
    }

    [HttpGet("center")]
    [Throttle(RequestsPerMinute = 60)]
    public async Task<ActionResult<object>> GetMapCenter()
    {
        CoordinateDto data = await Service.GetMapCenter();
        return Ok(data);
    }

    [HttpGet("{z:int}/{x:int}/{y:int}.mvt")]
    [Throttle(RequestsPerMinute = 600)]
    public async Task<IActionResult> GetTile(
        int z,
        int x,
        int y,
        [FromQuery] FilterQuery query,
        CancellationToken cancellationToken)
    {
        var mvtData = await Service.GetEntrancesMVTAsync(z, x, y, query, cancellationToken);
        // Response.Headers.Add("Cache-Control", "public, max-age=86400"); // cache for 1 day
        if (mvtData == null)
        {
            return NotFound();
        }

        return File(mvtData, "application/vnd.mapbox-vector-tile");
    }


    [HttpGet("lineplots/ids")]
    [Throttle(RequestsPerMinute = 600)]
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
    [Throttle(RequestsPerMinute = 1200)]
    public async Task<IActionResult> GetLinePlotById(
        [FromRoute] string plotId,
        CancellationToken cancellationToken)
    {
        var element = await Service.GetLinePlotGeoJson(
            plotId, cancellationToken);
        if (element == null)
            return NotFound();

        Response.Headers["Cache-Control"] = "private, no-cache";
        return new JsonResult(element.Value);
    }
    
    [HttpPost("hydrology/gages")]
    [Throttle(RequestsPerMinute = 60)]
    public async Task<ActionResult<IReadOnlyList<NearbyStreamGage>>> GetNearbyStreamGages(
        [FromBody] StreamGageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidOrigins(request.Origins) || request.DistanceMiles is <= 0 or > 50)
        {
            return BadRequest("Stream gage search parameters are outside the supported range.");
        }

        var gages = await Service.GetNearbyStreamGages(request, cancellationToken);
        Response.Headers["Cache-Control"] = "private, max-age=300";
        return Ok(gages);
    }

    [HttpGet("hydrology/gages/{siteCode}/observations")]
    [Throttle(RequestsPerMinute = 120)]
    public async Task<ActionResult<IReadOnlyList<StreamGageParameter>>> GetStreamGageObservations(
        string siteCode,
        [FromQuery] DateTimeOffset startDate,
        [FromQuery] DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        if (siteCode.Length is < 8 or > 15 ||
            !siteCode.All(char.IsDigit) ||
            endDate < startDate ||
            endDate - startDate > MaximumStreamGageObservationRange)
        {
            return BadRequest("USGS stream gage observation parameters are invalid or exceed the 90-day range limit.");
        }

        var observations = await Service.GetStreamGageObservations(
            siteCode,
            startDate,
            endDate,
            cancellationToken);
        Response.Headers["Cache-Control"] = "private, max-age=300";
        return Ok(observations);
    }

    [HttpGet("hydrology/gages/{siteCode}/peaks")]
    [Throttle(RequestsPerMinute = 120)]
    public async Task<ActionResult<StreamGagePeakSummary>> GetStreamGagePeakSummary(
        string siteCode,
        CancellationToken cancellationToken = default)
    {
        if (siteCode.Length is < 8 or > 15 || !siteCode.All(char.IsDigit))
        {
            return BadRequest("USGS stream gage site code is invalid.");
        }

        var summary = await Service.GetStreamGagePeakSummary(siteCode, cancellationToken);
        Response.Headers["Cache-Control"] = "private, max-age=86400";
        return Ok(summary);
    }

    [HttpGet("hydrology/gages/bounds")]
    [Throttle(RequestsPerMinute = 120)]
    public async Task<ActionResult<IReadOnlyList<StreamGageLocation>>> GetStreamGagesInBounds(
        [FromQuery] double north,
        [FromQuery] double south,
        [FromQuery] double east,
        [FromQuery] double west,
        CancellationToken cancellationToken = default)
    {
        if (north is < -90 or > 90 || south is < -90 or > 90 ||
            east is < -180 or > 180 || west is < -180 or > 180 ||
            north <= south || east <= west || north - south > 10 || east - west > 10)
        {
            return BadRequest("Stream gage bounds are outside the supported range.");
        }

        var gages = await Service.GetStreamGagesInBounds(
            north, south, east, west, cancellationToken);
        Response.Headers["Cache-Control"] = "private, max-age=300";
        return Ok(gages);
    }

    [HttpGet("hydrology/features/bounds")]
    [Throttle(RequestsPerMinute = 120)]
    public async Task<ActionResult<IReadOnlyList<HydrologyFeature>>> GetHydrologyFeaturesInBounds(
        [FromQuery] double north,
        [FromQuery] double south,
        [FromQuery] double east,
        [FromQuery] double west,
        CancellationToken cancellationToken = default)
    {
        if (north is < -90 or > 90 || south is < -90 or > 90 ||
            east is < -180 or > 180 || west is < -180 or > 180 ||
            north <= south || east <= west || north - south > 10 || east - west > 10)
        {
            return BadRequest("Hydrology bounds are outside the supported range.");
        }

        var features = await Service.GetHydrologyFeaturesInBounds(
            north, south, east, west, cancellationToken);
        Response.Headers["Cache-Control"] = "private, max-age=300";
        return Ok(features);
    }

    [HttpGet("geologic-maps")]
    [Throttle(RequestsPerMinute = 60)]
    public async Task<ActionResult<object>> GetMapCenter(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        CancellationToken cancellationToken)
    {
        var data = await Service.GetGeologicMaps(latitude, longitude, cancellationToken);
        Response.Headers["Cache-Control"] = "public, max-age=2592000"; // cache for 30 days
        return new JsonResult(data);
    }

    [HttpGet("ngmdb/{scale}/{z:int}/{x:int}/{y:int}")]
    [Throttle(RequestsPerMinute = 2400)]
    public async Task<IActionResult> GetNgmdbTile(
        string scale,
        int z,
        int x,
        int y,
        CancellationToken cancellationToken)
    {
        GeologicTileResult? tile;
        try
        {
            tile = await Service.GetGeologicTile(scale, z, x, y, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        if (tile == null)
        {
            return NotFound();
        }

        Response.Headers["Cache-Control"] = "public, max-age=2592000"; // cache for 30 days
        return File(tile.Content, tile.ContentType);
    }

    private static bool IsValidOrigins(IReadOnlyList<StreamGageSearchOrigin>? origins) =>
        origins is { Count: > 0 and <= 100 } &&
        origins.All(origin =>
            origin.Latitude is >= -90 and <= 90 &&
            origin.Longitude is >= -180 and <= 180);
}
