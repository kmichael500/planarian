using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities.RidgeWalker;
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

    [HttpPost]
    [Authorize(Policy = PermissionPolicyKey.PlanarianAdmin)]
    public async Task<ActionResult<string>> CreateAccount([FromBody] CreateAccountVm account,
        CancellationToken cancellationToken)
    {
        var result = await Service.CreateAccount(account, cancellationToken);

        return new JsonResult(result);
    }

    // dangerous
    [HttpDelete("reset")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult<string>> RestAccount(CancellationToken cancellationToken)
    {
        await Service.ResetAccount(cancellationToken);
        return Ok("Account reset finished.");
    }

    #region Misc Settings
    
    [HttpGet("settings")]
    public async Task<ActionResult<MiscAccountSettingsVm?>> GetSettings(CancellationToken cancellationToken)
    {
        var result = await Service.GetMiscAccountSettingsVm(cancellationToken);
        return Ok(result);
    }
    
    [HttpPut("settings")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult<MiscAccountSettingsVm>> UpdateSettings([FromBody] MiscAccountSettingsVm settings,
        CancellationToken cancellationToken)
    {
        var result = await Service.UpdateMiscAccountSettingsVm(settings, cancellationToken);
        return Ok(result);
    }
    
    // [HttpPost("settings")]
    // public async Task<ActionResult<MiscAccountSettingsVm>> CreateSettings([FromBody] MiscAccountSettingsVm settings,
    //     CancellationToken cancellationToken)
    // {
    //     var result = await Service.CreateOrUpdateMiscAccountSettingsVm(settings, cancellationToken);
    //     return Ok(result);
    // }

    

    #endregion

    #region Import

    [DisableRequestSizeLimit] //TODO
    [HttpPost("import/caves/file")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<IActionResult> ImportCavesFile(string? uuid, IFormFile file,
        CancellationToken cancellationToken)
    {
        var result =
            await _importService.AddTemporaryFileForImport(file.OpenReadStream(), file.FileName, uuid,
                cancellationToken);

        return new JsonResult(result);
    }

    [HttpPost("import/caves/process/{fileId:length(10)}")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<IActionResult> ImportCavesFileProcess(string fileId,
        bool isDryRun,
        CancellationToken cancellationToken)
    {
        var result = await _importService.ImportCavesFileProcess(fileId, isDryRun, cancellationToken);

        return new JsonResult(result);
    }

    [DisableRequestSizeLimit] //TODO
    [HttpPost("import/entrances/file")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<IActionResult> ImportEntrancesFile(string? uuid, IFormFile file,
        CancellationToken cancellationToken)
    {
        var result =
            await _importService.AddTemporaryFileForImport(file.OpenReadStream(), file.FileName, uuid,
                cancellationToken);

        return new JsonResult(result);
    }

    [DisableRequestSizeLimit] //TODO
    [HttpPost("import/file")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<IActionResult> ImportFile(string? uuid, string delimiterRegex, string idRegex, IFormFile file,
        CancellationToken cancellationToken, bool ignoreDuplicates)
    {
        await using var stream = file.OpenReadStream();
        var result =
            await _importService.AddFileForImport(stream, file.FileName, idRegex, delimiterRegex, ignoreDuplicates, uuid,
                cancellationToken);

        return new JsonResult(result);
    }

    [HttpPost("import/entrances/process/{fileId:length(10)}")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<IActionResult> ImportEntrancesFileProcess(string fileId,
        bool isDryRun,
        CancellationToken cancellationToken)
    {
        var result = await _importService.ImportEntrancesFileProcess(fileId, isDryRun, cancellationToken);

        return new JsonResult(result);
    }

    #endregion

    #region Tags

    [HttpGet("tags-table/{key}")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult<IEnumerable<TagTypeTableVm>>> GetTags(string key,
        CancellationToken cancellationToken)
    {
        var result = await Service.GetTagsForTable(key, cancellationToken);
        return Ok(result);
    }
    
    [HttpGet("counties-table/{stateId:length(10)}")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult<IEnumerable<TagTypeTableCountyVm>>> GetCountiesForTable(string stateId, CancellationToken cancellationToken)
    {
        var result = await Service.GetCountiesForTable(stateId, cancellationToken);
        return Ok(result);
    }
    
    [HttpGet("feature-settings")]
    public async Task<ActionResult<FeatureSettingVm>> GetFeatureSettings(CancellationToken cancellationToken)
    {
        var featureSettings = await Service.GetFeatureSettings(cancellationToken);

        return new JsonResult(featureSettings);
    }
    
    [HttpPost("feature-settings/{key}")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult<FeatureSettingVm>> UpdateFeatureSetting(FeatureKey key, bool isEnabled, CancellationToken cancellationToken)
    {
        await Service.UpdateFeatureSetting(key, isEnabled, cancellationToken);

        return new OkResult();
    }

    [HttpGet("states")]
    public async Task<ActionResult<IEnumerable<SelectListItem<string>>>> GetAllStates(CancellationToken cancellationToken)
    {
        var result = await Service.GetAllStates(cancellationToken);
        return Ok(result);
    }


    [HttpPost("states/{stateId:length(10)}/counties")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult<TagTypeTableCountyVm>> AddCounty(string stateId,
        [FromBody] CreateCountyVm county, CancellationToken cancellationToken)
    {
        var result = await Service.CreateOrUpdateCounty(stateId, county, null, cancellationToken);
        return Ok(result);
    }

    [HttpPut("states/{stateId:length(10)}/counties/{countyId:length(10)}")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult<TagTypeTableCountyVm>> AddCounty(string stateId, string countyId,
        [FromBody] CreateCountyVm county, CancellationToken cancellationToken)
    {
        var result = await Service.CreateOrUpdateCounty(stateId, county, countyId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("states/{stateId:length(10)}/counties/merge/{destinationCountyId:length(10)}")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult> MergeCounties([FromQuery] string sourceCountyIds, string destinationCountyId,
        CancellationToken cancellationToken)
    {
        var ids = sourceCountyIds.SplitAndTrim();
        await Service.MergeCounties(ids, destinationCountyId, cancellationToken);
        return Ok();
    }

    [HttpDelete("states/{stateId:length(10)}/counties")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult> DeleteCounties([FromQuery] string countyIds,
        CancellationToken cancellationToken)
    {
        var ids = countyIds.SplitAndTrim();
        await Service.DeleteCounties(ids, cancellationToken);
        return Ok();
    }

    [HttpPut("tags/{tagTypeId:length(10)}")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult<TagTypeTableVm>> UpdateTagTypeName(string tagTypeId,
        [FromBody] CreateEditTagTypeVm tag, CancellationToken cancellationToken)
    {
        var result = await Service.CreateOrUpdateTagType(tag, tagTypeId, cancellationToken);

        return new JsonResult(result);
    }

    [HttpPost("tags")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult<TagTypeTableVm>> CreateTagType(string tagTypeId, [FromBody] CreateEditTagTypeVm tag,
        CancellationToken cancellationToken)
    {
        var result = await Service.CreateOrUpdateTagType(tag, tagTypeId, cancellationToken);

        return new JsonResult(result);
    }

    [HttpDelete("tags")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult> DeleteTagTypes([FromQuery] string tagTypeIds, CancellationToken cancellationToken)
    {
        var ids = tagTypeIds.SplitAndTrim();
        var result = await Service.DeleteTagTypes(ids, cancellationToken);
        return Ok(result);
    }

    [HttpPost("tags/merge/{destinationTagTypeId:length(10)}")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult> MergeTagTypes([FromQuery] string sourceTagTypeIds, string destinationTagTypeId,
        CancellationToken cancellationToken)
    {
        var ids = sourceTagTypeIds.SplitAndTrim();
        await Service.MergeTagTypes(ids, destinationTagTypeId, cancellationToken);
        return Ok();
    }

    #endregion
}