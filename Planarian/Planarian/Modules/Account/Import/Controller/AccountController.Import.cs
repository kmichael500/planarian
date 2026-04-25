using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Account.Import.Models;
using Planarian.Shared.Attributes;

// ReSharper disable once CheckNamespace ( for partial class compatability )
namespace Planarian.Modules.Account.Controller;

public partial class AccountController
{
    private const long ImportUploadRequestSizeLimitBytes = 50 * 1024 * 1024; // 50 MB
    private const int ImportFileSessionRequestsPerMinute = 6000;

    [RequestSizeLimit(ImportUploadRequestSizeLimitBytes)]
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
        bool syncExisting,
        CancellationToken cancellationToken)
    {
        var result = await _importService.ImportCavesFileProcess(fileId, isDryRun, syncExisting, cancellationToken);

        return new JsonResult(result);
    }

    [RequestSizeLimit(ImportUploadRequestSizeLimitBytes)]
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

    [HttpPost("import/file/session")]
    [Throttle(RequestsPerMinute = ImportFileSessionRequestsPerMinute)]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult<ImportFileUploadSessionVm>> CreateImportFileUploadSession(
        [FromBody] ImportFileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _importService.CreateFileUploadSession(request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("import/file/session/{sessionId}")]
    [Throttle(RequestsPerMinute = ImportFileSessionRequestsPerMinute)]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<ActionResult<ImportFileUploadSessionVm>> UploadImportFileChunk(
        string sessionId,
        long offset,
        int chunkIndex,
        CancellationToken cancellationToken)
    {
        var contentLength = Request.ContentLength ?? 0;
        var result = await _importService.UploadFileChunk(
            sessionId,
            Request.Body,
            offset,
            chunkIndex,
            contentLength,
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("import/file/session/{sessionId}/finalize")]
    [Throttle(RequestsPerMinute = ImportFileSessionRequestsPerMinute)]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<IActionResult> FinalizeImportFileUploadSession(string sessionId, CancellationToken cancellationToken)
    {
        var result = await _importService.FinalizeFileUploadSession(sessionId, cancellationToken);
        result.RequestId = HttpContext.TraceIdentifier;

        if (result.IsSuccessful)
        {
            return Ok(result);
        }

        throw ApiExceptionDictionary.FromErrorCode(result.FailureCode, result.Message, result);
    }

    [HttpDelete("import/file/session/{sessionId}")]
    [Throttle(RequestsPerMinute = ImportFileSessionRequestsPerMinute)]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<IActionResult> CancelImportFileUploadSession(string sessionId, CancellationToken cancellationToken)
    {
        await _importService.CancelFileUploadSession(sessionId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("import/file/session")]
    [Throttle(RequestsPerMinute = ImportFileSessionRequestsPerMinute)]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<IActionResult> CancelActiveImportFileUploadSessions(CancellationToken cancellationToken)
    {
        await _importService.CancelActiveFileUploadSessions(cancellationToken);
        return NoContent();
    }

    [HttpPost("import/entrances/process/{fileId:length(10)}")]
    [Authorize(Policy = PermissionPolicyKey.Admin)]
    public async Task<IActionResult> ImportEntrancesFileProcess(string fileId,
        bool isDryRun,
        bool syncExisting,
        CancellationToken cancellationToken)
    {
        var result = await _importService.ImportEntrancesFileProcess(fileId, isDryRun, syncExisting, cancellationToken);

        return new JsonResult(result);
    }

}
