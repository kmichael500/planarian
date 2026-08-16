using Planarian.Library.Exceptions;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;

namespace Planarian.Modules.Caves.Services;

public sealed class CaveRevisionService
{
    private readonly CaveRepository _caves;
    private readonly CaveRevisionQueryRepository _revisions;
    private readonly CaveRevisionDiffService _diff = new();

    public CaveRevisionService(CaveRepository caves, CaveRevisionQueryRepository revisions)
    {
        _caves = caves;
        _revisions = revisions;
    }

    public async Task<CaveRevisionHistoryVm> ListAsync(string caveId, CancellationToken cancellationToken)
    {
        await RequireVisibleCaveAsync(caveId);
        var result = await _revisions.ListAsync(caveId, cancellationToken)
                     ?? throw ApiExceptionDictionary.NotFound("Cave");
        var items = result.Item2.Select((row, index) => Map(row, index + 1,
            row.Revision.Id == result.CurrentRevisionId)).Reverse().ToList();
        return new CaveRevisionHistoryVm(caveId, result.CurrentRevisionId, items);
    }

    public async Task<CaveRevisionComparisonVm> CompareAsync(string caveId, string revisionId,
        CancellationToken cancellationToken)
    {
        await RequireVisibleCaveAsync(caveId);
        var history = await _revisions.ListAsync(caveId, cancellationToken)
                      ?? throw ApiExceptionDictionary.NotFound("Cave");
        var ordered = history.Item2;
        var index = ordered.ToList().FindIndex(row => row.Revision.Id == revisionId);
        if (index < 0) throw ApiExceptionDictionary.NotFound("Cave revision");

        var currentRow = ordered[index];
        var previousRow = index == 0 ? null : ordered[index - 1];
        var current = CaveSnapshotJson.Deserialize(currentRow.Revision.SnapshotJson,
            currentRow.Revision.SnapshotSchemaVersion);
        var previous = previousRow is null ? null : CaveSnapshotJson.Deserialize(previousRow.Revision.SnapshotJson,
            previousRow.Revision.SnapshotSchemaVersion);

        return new CaveRevisionComparisonVm(
            Map(currentRow, index + 1, currentRow.Revision.Id == history.CurrentRevisionId),
            previousRow is null ? null : Map(previousRow, index, previousRow.Revision.Id == history.CurrentRevisionId),
            previous,
            current,
            previous is null ? null : Map(_diff.Compare(previous, current)));
    }

    private async Task RequireVisibleCaveAsync(string caveId)
    {
        if (await _caves.GetCave(caveId) is null) throw ApiExceptionDictionary.NotFound("Cave");
    }

    private static CaveRevisionListItemVm Map(CaveRevisionQueryRow row, int sequence, bool isCurrent) => new(
        row.Revision.Id, row.Revision.CaveId, sequence, row.Revision.CreatedOn,
        row.Revision.CreatedByUserId, row.ActorName, row.Revision.Source, row.Revision.Operation,
        row.Revision.ChangeRequestId, row.Revision.ImportBatchId, isCurrent);

    private static CaveRevisionDiffVm Map(CaveRevisionDiff diff) => new(
        diff.Scalars.Select(change => new CaveScalarChangeVm(change.Key, change.Value.Previous,
            change.Value.Current)).ToList(),
        diff.AddedTags, diff.RemovedTags, diff.AddedEntrances, diff.RemovedEntrances,
        diff.ChangedEntrances, diff.AddedFiles, diff.RemovedFiles, diff.ChangedFiles,
        diff.AddedLinePlots, diff.RemovedLinePlots, diff.ChangedLinePlots,
        diff.EntranceChanges.Select(change => new CaveEntranceChangeVm(change.EntranceId,
            change.Scalars.Select(pair => new CaveScalarChangeVm(pair.Key, pair.Value.Previous, pair.Value.Current)).ToList(),
            change.AddedTags, change.RemovedTags)).ToList(),
        diff.FileChanges.Select(change => new CaveFileChangeVm(change.FileId,
            change.Scalars.Select(pair => new CaveScalarChangeVm(pair.Key, pair.Value.Previous, pair.Value.Current)).ToList())).ToList(),
        diff.LinePlotChanges.Select(change => new CaveLinePlotChangeVm(change.LinePlotId,
            change.Scalars.Select(pair => new CaveScalarChangeVm(pair.Key, pair.Value.Previous, pair.Value.Current)).ToList())).ToList(),
        diff.ReferenceMetadataChanges);
}
