using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Database.Entities.RidgeWalker.ViewModels;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Files.Controllers;

[Route("api/files")]
public class FileController : PlanarianControllerBase<FileService>
{
    private readonly FileService _fileService;

    public FileController(RequestUser requestUser, TokenService tokenService, FileService service, FileService fileService) : base(requestUser,
        tokenService, service)
    {
        _fileService = fileService;
    }

    [HttpPut("multiple")]
    public async Task<IActionResult> UpdateFilesMetadata([FromBody] IEnumerable<EditFileMetadataVm> values,
        CancellationToken cancellationToken)
    {
        await Service.UpdateFilesMetadata(values, cancellationToken);
        return Ok();
    }

    [HttpGet("{id:length(10)}")]
    public async Task<IActionResult> GetFile(string id)
    {
        var file = await Service.GetFile(id);
        return new JsonResult(file);
    }

    [DisableRequestSizeLimit] //TODO
    [HttpPost("temporary")]
    public async Task<IActionResult> AddTemporaryFile(string? uuid, IFormFile file,
        CancellationToken cancellationToken)
    {
        var result = await _fileService.AddTemporaryFile(file.OpenReadStream(), file.FileName,
            FileTypeTagName.Other, cancellationToken, uuid);
        return new JsonResult(result);
    }

}

public class EditFileMetadataVm : EditFileMetadata;