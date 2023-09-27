using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Library.Extensions.String;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Account.Services;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Controller;

[Route(Route)]
[Authorize]
public class AccountController : PlanarianControllerBase<AccountService>
{
    private const string Route = "api/account";

    private readonly ImportService _importService;

    public AccountController(RequestUser requestUser, TokenService tokenService, AccountService service,
        CaveService caveService, ImportService importService) : base(
        requestUser, tokenService, service)
    {
        _importService = importService;
    }

    // dangerous
    [HttpDelete("reset")]
    public async Task<ActionResult<string>> RestAccount(CancellationToken cancellationToken)
    {
        await Service.ResetAccount(cancellationToken);
        return Ok();
    }

    #region Import

    [DisableRequestSizeLimit] //TODO
    [HttpPost("import/caves/file")]
    public async Task<IActionResult> ImportCavesFile(string? uuid, [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        var result =
            await _importService.AddTemporaryFileForImport(file.OpenReadStream(), file.FileName, uuid,
                cancellationToken);

        return new JsonResult(result);
    }

    [HttpPost("import/caves/process/{fileId:length(10)}")]
    public async Task<IActionResult> ImportCavesFileProcess(string fileId,
        CancellationToken cancellationToken)
    {
        var result = await _importService.ImportCavesFileProcess(fileId, cancellationToken);

        return new JsonResult(result);
    }

    [DisableRequestSizeLimit] //TODO
    [HttpPost("import/entrances/file")]
    public async Task<IActionResult> ImportEntrancesFile(string? uuid, [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        var result =
            await _importService.AddTemporaryFileForImport(file.OpenReadStream(), file.FileName, uuid,
                cancellationToken);

        return new JsonResult(result);
    }

    [HttpPost("import/entrances/process/{fileId:length(10)}")]
    public async Task<IActionResult> ImportEntrancesFileProcess(string fileId,
        CancellationToken cancellationToken)
    {
        var result = await _importService.ImportEntrancesFileProcess(fileId, cancellationToken);

        return new JsonResult(result);
    }

    #endregion

    #region Tags

    [HttpGet("tags-table/{key}")]
    public async Task<ActionResult<IEnumerable<TagTypeTableVm>>> GetTags(string key,
        CancellationToken cancellationToken)
    {
        var result = await Service.GetTagsForTable(key, cancellationToken);
        return Ok(result);
    }

    [HttpPut("tags/{tagTypeId:length(10)}")]
    public async Task<ActionResult<TagTypeTableVm>> UpdateTagTypeName(string tagTypeId,
        [FromBody] CreateEditTagTypeVm tag)
    {
        var result = await Service.CreateOrUpdateTagType(tag, tagTypeId);

        return new JsonResult(result);
    }

    [HttpPost("tags")]
    public async Task<ActionResult<TagTypeTableVm>> CreateTagType(string tagTypeId, [FromBody] CreateEditTagTypeVm tag)
    {
        var result = await Service.CreateOrUpdateTagType(tag, tagTypeId);

        return new JsonResult(result);
    }
    
    [HttpDelete("tags")]
    public async Task<ActionResult> DeleteTagType([FromQuery] string tagTypeIds)
    {
        var ids = tagTypeIds.SplitAndTrim();
        var result = await Service.DeleteTagTypes(ids);
        return Ok(result);
    }
    [HttpPost("tags/merge/{destinationTagTypeId:length(10)}")]
    public async Task<ActionResult> MergeTagTypes([FromQuery] string sourceTagTypeIds, string destinationTagTypeId)
    {
        var ids = sourceTagTypeIds.SplitAndTrim();
        await Service.MergeTagTypes(ids, destinationTagTypeId);
        return Ok();
    }


    #endregion
}