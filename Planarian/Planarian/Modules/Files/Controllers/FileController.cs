using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Files.Controllers;

[Route("api/files")]
[Authorize]
public class FileController : PlanarianControllerBase<FileService>
{
    public FileController(RequestUser requestUser, TokenService tokenService, FileService service) : base(requestUser,
        tokenService, service)
    {
    }

    [Authorize]
    [HttpPost("{fileId:length(10)}/access-ticket")]
    public async Task<ActionResult<FileAccessTicketVm>> CreateAccessTicket(
        string fileId,
        CancellationToken cancellationToken)
    {
        var result = await Service.CreateAccessTicket(fileId, cancellationToken);
        return new JsonResult(result);
    }

    [AllowAnonymous]
    [HttpGet("{fileId:length(10)}/view")]
    public async Task<IActionResult> ViewFile(string fileId, [FromQuery] string? ticket, CancellationToken cancellationToken)
    {
        var redirectUrl = await Service.GetSasUrl(fileId, false, ticket, cancellationToken);
        return Redirect(redirectUrl);
    }

    [AllowAnonymous]
    [HttpGet("{fileId:length(10)}/download")]
    public async Task<IActionResult> DownloadFile(string fileId, [FromQuery] string? ticket, CancellationToken cancellationToken)
    {
        var redirectUrl = await Service.GetSasUrl(fileId, true, ticket, cancellationToken);
        return Redirect(redirectUrl);
    }

    [HttpPut("multiple")]
    [Authorize(Policy = PermissionPolicyKey.Manager)]
    public async Task<IActionResult> UpdateFilesMetadata([FromBody] IEnumerable<EditFileMetadataVm> values,
        CancellationToken cancellationToken)
    {
        await Service.UpdateFilesMetadata(values, cancellationToken);
        return Ok();
    }
}

public class EditFileMetadataVm
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string FileTypeTagId { get; set; }
}

public class FileAccessTicketVm
{
    public string Ticket { get; set; } = null!;
}
