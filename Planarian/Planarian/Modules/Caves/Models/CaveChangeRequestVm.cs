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

public enum CaveProposalCountyNumberMode
{
    PreserveExisting,
    Manual,
    FirstAvailable,
    AutomaticNext
}

public sealed record CaveProposalCountyNumberStateVm(CaveProposalCountyNumberMode Mode, int? Number = null);

public static class CaveProposalCountyNumberPresentation
{
    public static CaveProposalCountyNumberStateVm Resolve(CaveProposalSnapshotV1 proposal,
        CavePublishedSnapshotV1 baseSnapshot)
    {
        if (proposal.CountyNumberIntent == CountyNumberIntent.Manual)
            return new CaveProposalCountyNumberStateVm(CaveProposalCountyNumberMode.Manual,
                proposal.RequestedCountyNumber);
        if (proposal.CountyId == baseSnapshot.County.Id)
            return new CaveProposalCountyNumberStateVm(CaveProposalCountyNumberMode.PreserveExisting,
                baseSnapshot.CountyNumber);
        return new CaveProposalCountyNumberStateVm(
            proposal.CountyNumberIntent == CountyNumberIntent.FirstAvailable
                ? CaveProposalCountyNumberMode.FirstAvailable
                : CaveProposalCountyNumberMode.AutomaticNext);
    }
}

public sealed record CaveProposalCountyNumberChangeVm(
    CaveProposalCountyNumberStateVm Previous,
    CaveProposalCountyNumberStateVm Current);

public sealed record CaveProposalVersionDetailVm(CavePublishedSnapshotV1 Base,
    CavePublishedSnapshotV1 Proposed, CaveRevisionDiffVm DiffFromBase,
    CavePublishedSnapshotV1? PreviousProposed, CaveRevisionDiffVm? DiffFromPreviousVersion,
    CaveProposalCountyNumberChangeVm? CountyNumberChange,
    bool BaseRevisionChanged, string? PreviousBaseRevisionId, string BaseRevisionId,
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
