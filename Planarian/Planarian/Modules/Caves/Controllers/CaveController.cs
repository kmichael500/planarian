using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Attributes;
using Planarian.Shared.Base;

namespace Planarian.Modules.Caves.Controllers;

[Route("api/caves")]
[Authorize]
public class CaveController : PlanarianControllerBase<CaveService>
{
    private readonly CaveRevisionService _revisionService;

    public CaveController(RequestUser requestUser, TokenService tokenService, CaveService service,
        CaveRevisionService revisionService) : base(requestUser, tokenService, service)
    {
        _revisionService = revisionService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CaveSearchVm>>> GetCaves([FromQuery] FilterQuery query)
    {
        var caves = await Service.GetCaves(query);

        return new JsonResult(caves);
    }


    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<CaveSearchVm>>> GetCavesSearch([FromQuery] FilterQuery query,
        [FromQuery] string? permissionKey = null)
    {
        var caves = await Service.GetCavesSearch(query, permissionKey);

        return new JsonResult(caves);
    }

    [HttpGet("export/gpx")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Authorize(Policy = PermissionPolicyKey.Export)]
    [Throttle(RequestsPerMinute = 5)]
    public async Task<ActionResult> ExportCavesGpx([FromQuery] FilterQuery query,
        [FromQuery] string? permissionKey = null)
    {
        var fileBytes = await Service.ExportCavesGpx(query, permissionKey);

        var fileName = $"Caves {DateTime.UtcNow.Ticks}";
        return File(fileBytes, "application/gpx+xml", fileName);
    }

    [HttpGet("export/csv")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Authorize(Policy = PermissionPolicyKey.Export)]
    [Throttle(RequestsPerMinute = 5)]
    public async Task<ActionResult> ExportCavesCsv([FromQuery] FilterQuery query,
        [FromQuery] string? permissionKey = null)
    {
        var fileBytes = await Service.ExportCavesCsv(query, permissionKey);

        var fileName = $"Caves_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(fileBytes, "text/csv", fileName);
    }

    [HttpGet("{caveId:length(10)}")]
    public async Task<ActionResult<CaveVm>> GetCave(string caveId)
    {
        var cave = await Service.GetCave(caveId);

        return new JsonResult(cave);
    }

    [HttpGet("{caveId:length(10)}/edit-context")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<CaveEditAuthoringContextVm>> GetEditAuthoringContext(string caveId,
        CancellationToken cancellationToken) =>
        new JsonResult(await Service.GetEditAuthoringContextAsync(caveId, cancellationToken));

    [HttpGet("{caveId:length(10)}/revisions")]
    public async Task<ActionResult<CaveRevisionHistoryVm>> GetRevisions(string caveId,
        CancellationToken cancellationToken) =>
        new JsonResult(await _revisionService.ListAsync(caveId, cancellationToken));

    [HttpGet("{caveId:length(10)}/revisions/{revisionId:length(10)}")]
    public async Task<ActionResult<CaveRevisionComparisonVm>> GetRevision(string caveId, string revisionId,
        CancellationToken cancellationToken) =>
        new JsonResult(await _revisionService.CompareAsync(caveId, revisionId, cancellationToken));

    [HttpGet("counties/{countyId:length(10)}/next-number")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<ActionResult<int>> GetNextCountyNumber(string countyId,
        [FromQuery] bool useFirstAvailableCountyNumber = false)
    {
        var countyNumber = await Service.GetNextCountyNumber(countyId, useFirstAvailableCountyNumber);

        return new JsonResult(countyNumber);
    }

    [HttpGet("counties/{countyId:length(10)}/county-numbers/{countyNumber:int}/in-use")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<ActionResult<bool>> IsCountyNumberInUse(string countyId, int countyNumber,
        [FromQuery] string? caveId = null)
    {
        var isInUse = await Service.IsCountyNumberInUse(countyId, countyNumber, caveId);

        return new JsonResult(isInUse);
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<ActionResult<string>> AddCave([FromBody] AddCaveVm cave, CancellationToken cancellationToken)
    {
        var result = await Service.AddCave(cave, cancellationToken);

        return new JsonResult(result);
    }

    [HttpPut]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<ActionResult<string>> UpdateCave([FromBody] AddCaveVm cave, CancellationToken cancellationToken)
    {
        try
        {
            return new JsonResult(await Service.AddCave(cave, cancellationToken));
        }
        catch (CaveRevisionConflictException conflict)
        {
            return Conflict(new CaveProposalAuthoringConflictVm(
                CaveProposalAuthoringConflictKind.PublishedCaveChanged,
                conflict.ExpectedRevisionId ?? string.Empty,
                conflict.ActualRevisionId));
        }
    }

    [HttpDelete("{caveId:length(10)}")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<ActionResult> DeleteCave(string caveId, CancellationToken cancellationToken)
    {
        await Service.DeleteCave(caveId, cancellationToken);

        return new OkResult();
    }

    [HttpPost("{caveId:length(10)}/archive")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<ActionResult> ArchiveCave(string caveId)
    {
        await Service.ArchiveCave(caveId);

        return new OkResult();
    }

    [HttpPost("{caveId:length(10)}/unarchive")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<ActionResult> UnarchiveCave(string caveId)
    {
        await Service.UnarchiveCave(caveId);

        return new OkResult();
    }

    #region Favorites
    
    [HttpGet("{caveId:length(10)}/favorite")]
    public async Task<ActionResult<FavoriteVm>> GetFavoriteCaves(string caveId)
    {
        var caves = await Service.GetFavoriteCave(caveId);

        return new JsonResult(caves);
    }

    [HttpPost("{caveId:length(10)}/favorite")]
    public async Task<ActionResult> FavoriteCave(string caveId)
    {
        await Service.FavoriteCave(caveId);

        return new OkResult();
    }

    [HttpDelete("{caveId:length(10)}/favorite")]
    public async Task<ActionResult> UnfavoriteCave(string caveId)
    {
        await Service.UnfavoriteCave(caveId);

        return new OkResult();
    }

    #endregion

}
