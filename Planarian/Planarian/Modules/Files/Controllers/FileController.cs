// using System.ComponentModel.DataAnnotations;
// using Microsoft.AspNetCore.Mvc;
// using Planarian.Model.Shared;
// using Planarian.Modules.Authentication.Services;
// using Planarian.Modules.Files.Services;
// using Planarian.Shared.Base;
//
// namespace Planarian.Modules.Files.Controllers;
//
// public class FileController : PlanarianControllerBase<FileService>
// {
//     public FileController(RequestUser requestUser, TokenService tokenService, FileService service) : base(requestUser,
//         tokenService, service)
//     {
//     }
//
//     [HttpPost("caves")]
//     public async Task<IActionResult> UploadCaveFile(public string caveId, [FromForm] IFormFile file)
//     {
//         var result = await Service.UploadCaveFile(f)
//         return Ok();
//     }
//
// }
//
// public class FileInformation
// {
//     [MaxLength(PropertyLength.Name)] public string DisplayName { get; set; }
//     public string FileTypeKey { get; set; }
//     public string? CaveId { get; set; }
//     public Guid Guid { get; set; }
// }
//
// public class FileTypeKey
// {
//     public const string Map = "map";
// }