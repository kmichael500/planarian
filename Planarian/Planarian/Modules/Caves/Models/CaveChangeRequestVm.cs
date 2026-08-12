using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;

namespace Planarian.Modules.Caves.Models;

public sealed record CaveChangeRequestSummaryVm(
    string Id,
    string CaveId,
    string CaveName,
    bool CaveExists,
    CaveChangeRequestStatus Status,
    string SubmitterUserId,
    string? SubmitterName,
    DateTime SubmittedOn,
    DateTime? UpdatedOn,
    DateTime? ReviewedOn,
    string? ReviewerUserId,
    string? ReviewerName,
    string? ReviewerNotes,
    string OriginalBaseRevisionId,
    string ProposalBaseRevisionId,
    string? CurrentRevisionId,
    string CurrentProposalVersionId,
    bool IsStale,
    string? ApprovedRevisionId,
    bool CanEdit,
    bool CanReview);

public sealed record CaveChangeRequestDetailVm(
    CaveChangeRequestSummaryVm Request,
    CavePublishedSnapshotV1 Base,
    CavePublishedSnapshotV1 Proposed,
    CavePublishedSnapshotV1 Current,
    CaveRevisionDiffVm Diff,
    CaveRevisionDiffVm? PublishedSinceBase,
    IReadOnlyList<CaveProposalVersionVm> Versions,
    CountyNumberIntent CountyNumberIntent,
    int? RequestedCountyNumber,
    IReadOnlyList<CaveFileSnapshotV1> ActiveStagedFiles);

public sealed record CaveChangePreviewVm(CavePublishedSnapshotV1 Base,
    CavePublishedSnapshotV1 Proposed, CaveRevisionDiffVm Diff,
    CountyNumberIntent CountyNumberIntent, int? RequestedCountyNumber);

public sealed record CreateCaveChangeRequestVm(AddCaveVm Cave, string ExpectedBaseRevisionId);

public sealed record CaveProposalAuthoringContextVm(CaveVm Cave, string ExpectedBaseRevisionId);

public sealed record ReviseCaveChangeRequestVm(AddCaveVm Cave, string ExpectedBaseRevisionId,
    string ExpectedProposalVersionId, bool AgainstCurrent);

public enum CaveProposalAuthoringConflictKind
{
    PublishedCaveChanged,
    ActiveProposalVersionChanged
}

public sealed record CaveProposalAuthoringConflictVm(CaveProposalAuthoringConflictKind ConflictKind,
    string ExpectedId, string? ActualId);

public sealed record CaveProposalVersionVm(string Id, string? PreviousProposalVersionId,
    string BaseRevisionId, string? CreatedByUserId, string? CreatedByName, DateTime CreatedOn, bool IsCurrent);

public sealed record CaveProposalVersionDetailVm(CavePublishedSnapshotV1 Base,
    CavePublishedSnapshotV1 Proposed, CaveRevisionDiffVm Diff,
    CountyNumberIntent CountyNumberIntent, int? RequestedCountyNumber,
    IReadOnlyList<string> UnavailableStagedFileIds);

public sealed record CaveChangeRequestDecisionRequestVm(string? Notes, string ExpectedProposalVersionId);

public enum CaveChangeRequestDecisionResult
{
    Approved,
    Rejected,
    Conflict
}

public sealed record CaveChangeRequestDecisionVm(CaveChangeRequestDecisionResult Result,
    string RequestId, string? PublishedRevisionId, string? CurrentRevisionId);
