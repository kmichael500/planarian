using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Account.Archive.Models;
using Planarian.Modules.Account.Services;
using Planarian.Modules.Files.Models;
using Planarian.Shared.Attributes;
using Planarian.Shared.Base;

// ReSharper disable once CheckNamespace ( for partial class compatability )
namespace Planarian.Modules.Account.Controller;

public partial class AccountController : PlanarianControllerBase<AccountService>
{
    [HttpPost("archive")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public IActionResult StartArchive()
    {
        Service.StartArchive();
        return Ok();
    }

    [HttpPost("archive/cancel")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public IActionResult CancelArchive()
    {
        Service.CancelArchive();
        return Ok();
    }

    [HttpGet("archive/status")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public ActionResult<ArchiveProgressVm?> GetArchiveStatus()
    {
        var result = Service.GetArchiveStatus();
        return Ok(result);
    }

    [HttpGet("archive/list")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult<IEnumerable<ArchiveListItemVm>>> GetRecentArchives(CancellationToken cancellationToken)
    {
        var result = await Service.GetRecentArchives(cancellationToken);
        return Ok(result);
    }

    [HttpPost("archive/download")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    [Throttle]
    public async Task<ActionResult<FileAccessUrlVm>> DownloadArchive(
        [FromQuery] string blobKey,
        CancellationToken cancellationToken)
    {
        var result = await Service.CreateArchiveDownloadUrl(blobKey, cancellationToken);
        return new JsonResult(result);
    }

    [HttpDelete("archive")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<IActionResult> DeleteArchive(string blobKey, CancellationToken cancellationToken)
    {
        await Service.DeleteArchive(blobKey, cancellationToken);
        return Ok();
    }
}
