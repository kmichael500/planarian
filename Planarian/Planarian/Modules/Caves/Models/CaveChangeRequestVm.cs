using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;

namespace Planarian.Modules.Caves.Models;

public sealed record CaveChangeRequestSummaryVm(
    string Id,
    string CaveId,
    string CaveName,
    CaveChangeRequestStatus Status,
    string SubmitterUserId,
    string? SubmitterName,
    DateTime SubmittedOn,
    DateTime? UpdatedOn,
    DateTime? ReviewedOn,
    string? ReviewerUserId,
    string? ReviewerName,
    string? ReviewerNotes,
    string BaseRevisionId,
    string? CurrentRevisionId,
    string CurrentProposalVersionId,
    bool IsStale,
    string? ApprovedRevisionId,
    bool CanEdit);

public sealed record CaveChangeRequestDetailVm(
    CaveChangeRequestSummaryVm Request,
    CavePublishedSnapshotV1 Base,
    CavePublishedSnapshotV1 Proposed,
    CavePublishedSnapshotV1 Current,
    CaveRevisionDiffVm Diff,
    CaveRevisionDiffVm? PublishedSinceBase,
    IReadOnlyList<CaveProposalVersionVm> Versions);

public sealed record CaveProposalVersionVm(string Id, string? PreviousProposalVersionId,
    string? CreatedByUserId, string? CreatedByName, DateTime CreatedOn, bool IsCurrent);

public sealed record RejectCaveChangeRequestVm(string? Notes);

public enum CaveChangeRequestDecisionResult
{
    Approved,
    Rejected,
    Conflict
}

public sealed record CaveChangeRequestDecisionVm(CaveChangeRequestDecisionResult Result,
    string RequestId, string? PublishedRevisionId, string? CurrentRevisionId);
