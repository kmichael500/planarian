using Microsoft.AspNetCore.Mvc;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Files.Controllers;

[Route("api/files")]
public class FileController : PlanarianControllerBase<FileService>
{
    public FileController(RequestUser requestUser, TokenService tokenService, FileService service) : base(requestUser,
        tokenService, service)
    {
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
    
}
public class EditFileMetadataVm
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string FileTypeTagId { get; set; }
}