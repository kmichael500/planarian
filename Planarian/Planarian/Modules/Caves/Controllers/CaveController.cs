using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpGet("{caveId:length(10)}")]
    public async Task<ActionResult<CaveVm>> GetCave(string caveId)
    {
        var cave = await Service.GetCave(caveId);

        return new JsonResult(cave);
    }

    [HttpPost]
    public async Task<ActionResult<string>> AddCave([FromBody] AddCaveVm cave)
    {
        var result = await Service.AddCave(cave);

        return new JsonResult(result);
    }

    [HttpPut]
    public async Task<ActionResult<string>> UpdateCave([FromBody] AddCaveVm cave)
    {
        var result = await Service.AddCave(cave);

        return new JsonResult(result);
    }
    
    [DisableRequestSizeLimit] //TODO
    [HttpPost("{caveId:length(10)}/files")]
    public async Task<IActionResult> UploadCaveFile(string caveId, string? uuid, [FromForm] IFormFile file)
    {
        var result = await _fileService.UploadCaveFile(file.OpenReadStream(), caveId, file.FileName, uuid);

        // return Ok();
        return new JsonResult(result);
    }

    [HttpDelete("{caveId:length(10)}")]
    public async Task<ActionResult> DeleteCave(string caveId)
    {
        await Service.DeleteCave(caveId);

        return new OkResult();
    }
    
    [HttpPost("{caveId:length(10)}/archive")]
    public async Task<ActionResult> ArchiveCave(string caveId)
    {
        await Service.ArchiveCave(caveId);

        return new OkResult();
    }
    [HttpPost("{caveId:length(10)}/unarchive")]
    public async Task<ActionResult> UnarchiveCave(string caveId)
    {
        await Service.UnarchiveCave(caveId);

        return new OkResult();
    }

    #region Import

    [DisableRequestSizeLimit] //TODO
    [HttpPost("import-caves")]
    public async Task<IActionResult> ImportCaves(string? uuid, [FromForm] IFormFile file)
    {
        FileVm result = await Service.ImportCaves(file.OpenReadStream(), file.FileName, uuid);

        return new JsonResult(result);
    }

    #endregion
}