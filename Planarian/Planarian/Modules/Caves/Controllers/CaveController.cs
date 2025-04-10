using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Caves.Controllers;

[Route("api/caves")]
[Authorize]
public class CaveController : PlanarianControllerBase<CaveService>
{
    private readonly FileService _fileService;

    public CaveController(RequestUser requestUser, TokenService tokenService, CaveService service,
        FileService fileService) : base(requestUser,
        tokenService, service)
    {
        _fileService = fileService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CaveVm>>> GetCaves([FromQuery] FilterQuery query)
    {
        var caves = await Service.GetCaves(query);

        return new JsonResult(caves);
    }


    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<CaveVm>>> GetCavesSearch([FromQuery] FilterQuery query,
        [FromQuery] string? permissionKey = null)
    {
        var caves = await Service.GetCavesSearch(query, permissionKey);

        return new JsonResult(caves);
    }

    [HttpGet("export/gpx")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Authorize(Policy = PermissionPolicyKey.Export)]
    public async Task<ActionResult> ExportCavesGpx([FromQuery] FilterQuery query,
        [FromQuery] string? permissionKey = null)
    {
        var fileBytes = await Service.ExportCavesGpx(query, permissionKey);

        var fileName = $"Caves {DateTime.UtcNow.Ticks}";
        return File(fileBytes, "application/gpx+xml", fileName);
    }

    [HttpGet("{caveId:length(10)}")]
    public async Task<ActionResult<CaveVm>> GetCave(string caveId)
    {
        var cave = await Service.GetCave(caveId);

        return new JsonResult(cave);
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
        var result = await Service.AddCave(cave, cancellationToken);

        return new JsonResult(result);
    }

    [DisableRequestSizeLimit] //TODO
    [HttpPost("{caveId:length(10)}/files")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<IActionResult> UploadCaveFile(string caveId, string? uuid, IFormFile file,
        CancellationToken cancellationToken)
    {
        var result =
            await _fileService.UploadCaveFile(file.OpenReadStream(), caveId, file.FileName, cancellationToken, uuid);

        // return Ok();
        return new JsonResult(result);
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

    [HttpGet("favorites")]
    public async Task<ActionResult<PagedResult<CaveVm>>> GetFavoriteCaves([FromQuery] FilterQuery query)
    {
        var caves = await Service.GetFavoriteCaves(query);

        return new JsonResult(caves);
    }

    [HttpPost("{caveId:length(10)}/favorite")]
    public async Task<ActionResult> FavoriteCave(string caveId, CreateFavoriteVm values)
    {
        await Service.FavoriteCave(caveId, values);

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