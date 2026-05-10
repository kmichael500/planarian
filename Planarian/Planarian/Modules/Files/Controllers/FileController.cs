using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Attributes;
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

    [HttpGet("{fileId:length(10)}/view")]
    [Throttle]
    public async Task<IActionResult> ViewFile(
        string fileId,
        CancellationToken cancellationToken)
    {
        var result = await Service.CreateFileResponse(fileId, false, cancellationToken);
        return await CreateFileResult(result);
    }

    [HttpGet("{fileId:length(10)}/download")]
    [Throttle]
    public async Task<IActionResult> DownloadFile(string fileId, CancellationToken cancellationToken)
    {
        var result = await Service.CreateFileResponse(fileId, true, cancellationToken);
        return await CreateFileResult(result);
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
