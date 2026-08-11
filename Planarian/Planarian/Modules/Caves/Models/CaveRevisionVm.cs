using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;

namespace Planarian.Modules.Caves.Models;

public sealed record CaveRevisionListItemVm(
    string Id,
    string CaveId,
    int Sequence,
    DateTime PublishedOn,
    string? ActorUserId,
    string? ActorName,
    CaveRevisionSource Source,
    CaveRevisionOperation Operation,
    string? ChangeRequestId,
    string? ImportBatchId,
    bool IsCurrent);

public sealed record CaveRevisionHistoryVm(string CaveId, string? CurrentRevisionId,
    IReadOnlyList<CaveRevisionListItemVm> Revisions);

public sealed record CaveRevisionComparisonVm(
    CaveRevisionListItemVm Revision,
    CaveRevisionListItemVm? PreviousRevision,
    CavePublishedSnapshotV1? Previous,
    CavePublishedSnapshotV1 Current,
    CaveRevisionDiffVm? Diff);

public sealed record CaveScalarChangeVm(string Path, object? Previous, object? Current);

public sealed record CaveRevisionDiffVm(
    IReadOnlyList<CaveScalarChangeVm> Scalars,
    IReadOnlyList<SnapshotTagReference> AddedTags,
    IReadOnlyList<SnapshotTagReference> RemovedTags,
    IReadOnlyList<string> AddedEntrances,
    IReadOnlyList<string> RemovedEntrances,
    IReadOnlyList<string> ChangedEntrances,
    IReadOnlyList<string> AddedFiles,
    IReadOnlyList<string> RemovedFiles,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<ReferenceMetadataChange> ReferenceMetadataChanges);
