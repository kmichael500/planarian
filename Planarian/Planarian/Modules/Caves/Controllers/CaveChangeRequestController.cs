using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Caves.Services;

namespace Planarian.Modules.Caves.Controllers;

[ApiController]
[Route("api/cave-change-requests")]
[Authorize]
public sealed class CaveChangeRequestController : ControllerBase
{
    private readonly CaveChangeRequestService _service;

    public CaveChangeRequestController(CaveChangeRequestService service) => _service = service;

    [HttpPost("caves/{caveId:length(10)}")]
    public async Task<ActionResult<string>> Create(string caveId, CreateCaveChangeRequestVm request,
        CancellationToken cancellationToken)
    {
        try
        {
            return new JsonResult(await _service.CreateAsync(caveId, request.Cave,
                request.ExpectedBaseRevisionId, cancellationToken));
        }
        catch (CaveRevisionConflictException conflict)
        {
            return PublishedCaveConflict(conflict);
        }
    }

    [HttpPost("caves/{caveId:length(10)}/authoring-context")]
    public async Task<ActionResult<CaveProposalAuthoringContextVm>> InitializeAuthoringContext(string caveId,
        CancellationToken cancellationToken) =>
        new JsonResult(await _service.GetAuthoringContextAsync(caveId, cancellationToken));

    [HttpPost("caves/{caveId:length(10)}/preview")]
    public async Task<ActionResult<CaveChangePreviewVm>> Preview(string caveId, CreateCaveChangeRequestVm request,
        CancellationToken cancellationToken)
    {
        try
        {
            return new JsonResult(await _service.PreviewAsync(caveId, request.Cave,
                request.ExpectedBaseRevisionId, cancellationToken));
        }
        catch (CaveRevisionConflictException conflict)
        {
            return PublishedCaveConflict(conflict);
        }
    }

    [HttpPost("{requestId:length(10)}/versions/preview")]
    public async Task<ActionResult<CaveChangePreviewVm>> PreviewVersion(string requestId,
        ReviseCaveChangeRequestVm request, CancellationToken cancellationToken)
    {
        try
        {
            return new JsonResult(await _service.PreviewVersionAsync(requestId, request.Cave,
                request.AgainstCurrent, request.ExpectedBaseRevisionId, request.ExpectedProposalVersionId,
                cancellationToken));
        }
        catch (CaveRevisionConflictException conflict)
        {
            return PublishedCaveConflict(conflict);
        }
        catch (CaveProposalVersionConflictException conflict)
        {
            return ProposalVersionConflict(conflict);
        }
    }

    [HttpPost("{requestId:length(10)}/versions")]
    public async Task<ActionResult<string>> AddVersion(string requestId, ReviseCaveChangeRequestVm request,
        CancellationToken cancellationToken)
    {
        try
        {
            return new JsonResult(await _service.AddVersionAsync(requestId, request.Cave,
                request.AgainstCurrent, request.ExpectedBaseRevisionId, request.ExpectedProposalVersionId,
                cancellationToken));
        }
        catch (CaveRevisionConflictException conflict)
        {
            return PublishedCaveConflict(conflict);
        }
        catch (CaveProposalVersionConflictException conflict)
        {
            return ProposalVersionConflict(conflict);
        }
    }

    [HttpPost("{requestId:length(10)}/files")]
    [RequestSizeLimit(550L * 1024 * 1024)]
    public async Task<ActionResult> StageFile(string requestId, string? uuid, IFormFile file,
        CancellationToken cancellationToken) =>
        new JsonResult(await _service.StageFileAsync(requestId, file.OpenReadStream(), file.FileName, uuid,
            cancellationToken));

    [HttpGet("{requestId:length(10)}/files/{fileId:length(10)}")]
    public async Task<IActionResult> DownloadStagedFile(string requestId, string fileId,
        CancellationToken cancellationToken)
    {
        var file = await _service.OpenStagedFileAsync(requestId, fileId, cancellationToken);
        return File(file.Stream, "application/octet-stream", file.FileName);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<CaveChangeRequestSummaryVm>>> Mine(
        CancellationToken cancellationToken) =>
        new JsonResult(await _service.ListMineAsync(cancellationToken));

    [HttpGet("review")]
    public async Task<ActionResult<IReadOnlyList<CaveChangeRequestSummaryVm>>> ReviewQueue(
        CancellationToken cancellationToken) =>
        new JsonResult(await _service.ListForReviewAsync(cancellationToken));

    [HttpGet("{requestId:length(10)}")]
    public async Task<ActionResult<CaveChangeRequestDetailVm>> Get(string requestId,
        CancellationToken cancellationToken) =>
        new JsonResult(await _service.GetAsync(requestId, cancellationToken));

    [HttpGet("{requestId:length(10)}/versions/{versionId:length(10)}")]
    public async Task<ActionResult<CaveProposalVersionDetailVm>> GetVersion(string requestId, string versionId,
        CancellationToken cancellationToken) =>
        new JsonResult(await _service.GetVersionAsync(requestId, versionId, cancellationToken));

    [HttpPost("{requestId:length(10)}/approve")]
    public async Task<ActionResult<CaveChangeRequestDecisionVm>> Approve(string requestId,
        CaveChangeRequestDecisionRequestVm request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.ApproveAsync(requestId, request.ExpectedProposalVersionId,
                request.Notes, cancellationToken);
            return new JsonResult(result);
        }
        catch (CaveRevisionConflictException conflict)
        {
            return PublishedCaveConflict(conflict);
        }
        catch (CaveProposalVersionConflictException conflict)
        {
            return ProposalVersionConflict(conflict);
        }
    }

    [HttpPost("{requestId:length(10)}/reject")]
    public async Task<ActionResult<CaveChangeRequestDecisionVm>> Reject(string requestId,
        CaveChangeRequestDecisionRequestVm request, CancellationToken cancellationToken)
    {
        try
        {
            return new JsonResult(await _service.RejectAsync(requestId, request.ExpectedProposalVersionId,
                request.Notes, cancellationToken));
        }
        catch (CaveProposalVersionConflictException conflict)
        {
            return ProposalVersionConflict(conflict);
        }
    }

    private ConflictObjectResult PublishedCaveConflict(CaveRevisionConflictException conflict) =>
        Conflict(new CaveProposalAuthoringConflictVm(CaveProposalAuthoringConflictKind.PublishedCaveChanged,
            conflict.ExpectedRevisionId ?? string.Empty, conflict.ActualRevisionId));

    private ConflictObjectResult ProposalVersionConflict(CaveProposalVersionConflictException conflict) =>
        Conflict(new CaveProposalAuthoringConflictVm(CaveProposalAuthoringConflictKind.ActiveProposalVersionChanged,
            conflict.ExpectedProposalVersionId, conflict.ActualProposalVersionId));
}
