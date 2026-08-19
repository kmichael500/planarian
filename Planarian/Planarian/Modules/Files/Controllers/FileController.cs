using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Attributes;
using Planarian.Shared.Base;
using Planarian.Shared.Services;

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

    [HttpPost("staged")]
    [RequestSizeLimit(550L * 1024 * 1024)]
    public async Task<ActionResult<FileVm>> StageAuthoringFile(string? uuid, IFormFile file,
        CancellationToken cancellationToken) =>
        new JsonResult(await Service.StageAuthoringFile(file.OpenReadStream(), file.FileName,
            cancellationToken, uuid));

    [HttpGet("staged/{fileId:length(10)}")]
    public Task<IActionResult> DownloadAuthoringStagedFile(string fileId, CancellationToken cancellationToken) =>
        OpenAuthoringStagedFileResponse(fileId, requestedDownload: true, cancellationToken);

    [HttpGet("staged/{fileId:length(10)}/view")]
    public Task<IActionResult> ViewAuthoringStagedFile(string fileId, CancellationToken cancellationToken) =>
        OpenAuthoringStagedFileResponse(fileId, requestedDownload: false, cancellationToken);

    [HttpGet("staged/{fileId:length(10)}/download")]
    public Task<IActionResult> DownloadAuthoringStagedFileExplicit(string fileId, CancellationToken cancellationToken) =>
        OpenAuthoringStagedFileResponse(fileId, requestedDownload: true, cancellationToken);

    private async Task<IActionResult> OpenAuthoringStagedFileResponse(string fileId, bool requestedDownload,
        CancellationToken cancellationToken)
    {
        var file = await Service.OpenAuthoringStagedFileAsync(fileId, cancellationToken);
        var response = FileResponsePolicy.Resolve(file.FileName, "application/octet-stream", requestedDownload);
        return response.ForceDownload
            ? File(file.Stream, response.ContentType, file.FileName)
            : File(file.Stream, response.ContentType);
    }

    [HttpDelete("staged/{fileId:length(10)}")]
    public async Task<IActionResult> DeleteAuthoringStagedFile(string fileId,
        CancellationToken cancellationToken)
    {
        await Service.DeleteUnpublishedFileAsync(fileId, cancellationToken);
        return NoContent();
    }


}

public class EditFileMetadataVm
{
    public string Id { get; set; }
    [Required, MaxLength(PropertyLength.FileName)] public string Name { get; set; }
    public string FileTypeTagId { get; set; }
}
