namespace Planarian.Model.Database.Revisions;

public sealed record CaveRevisionDiff(
    IReadOnlyDictionary<string, (object? Previous, object? Current)> Scalars,
    IReadOnlyList<SnapshotTagReference> AddedTags,
    IReadOnlyList<SnapshotTagReference> RemovedTags,
    IReadOnlyList<string> AddedEntrances,
    IReadOnlyList<string> RemovedEntrances,
    IReadOnlyList<string> ChangedEntrances,
    IReadOnlyList<string> AddedFiles,
    IReadOnlyList<string> RemovedFiles);

public sealed class CaveRevisionDiffService
{
    public CaveRevisionDiff Compare(CavePublishedSnapshotV1 previous, CavePublishedSnapshotV1 current)
    {
        var scalars = new Dictionary<string, (object?, object?)>();
        AddScalar(nameof(CavePublishedSnapshotV1.Name), previous.Name, current.Name);
        AddScalar(nameof(CavePublishedSnapshotV1.AlternateNames), previous.AlternateNames, current.AlternateNames);
        AddScalar(nameof(CavePublishedSnapshotV1.State), previous.State, current.State);
        AddScalar(nameof(CavePublishedSnapshotV1.County), previous.County, current.County);
        AddScalar(nameof(CavePublishedSnapshotV1.CountyNumber), previous.CountyNumber, current.CountyNumber);
        AddScalar(nameof(CavePublishedSnapshotV1.LengthFeet), previous.LengthFeet, current.LengthFeet);
        AddScalar(nameof(CavePublishedSnapshotV1.DepthFeet), previous.DepthFeet, current.DepthFeet);
        AddScalar(nameof(CavePublishedSnapshotV1.MaxPitDepthFeet), previous.MaxPitDepthFeet, current.MaxPitDepthFeet);
        AddScalar(nameof(CavePublishedSnapshotV1.NumberOfPits), previous.NumberOfPits, current.NumberOfPits);
        AddScalar(nameof(CavePublishedSnapshotV1.Narrative), previous.Narrative, current.Narrative);
        AddScalar(nameof(CavePublishedSnapshotV1.ReportedOn), previous.ReportedOn, current.ReportedOn);
        AddScalar(nameof(CavePublishedSnapshotV1.IsArchived), previous.IsArchived, current.IsArchived);

        var oldTags = previous.Tags.ToDictionary(t => t.TagTypeId);
        var newTags = current.Tags.ToDictionary(t => t.TagTypeId);
        var addedTags = current.Tags.Where(t => !oldTags.ContainsKey(t.TagTypeId)).OrderBy(t => t.TagTypeId).ToList();
        var removedTags = previous.Tags.Where(t => !newTags.ContainsKey(t.TagTypeId)).OrderBy(t => t.TagTypeId).ToList();
        var oldEntrances = previous.Entrances.ToDictionary(e => e.Id);
        var newEntrances = current.Entrances.ToDictionary(e => e.Id);
        var oldFiles = previous.Files.ToDictionary(e => e.Id);
        var newFiles = current.Files.ToDictionary(e => e.Id);

        return new CaveRevisionDiff(scalars, addedTags, removedTags,
            newEntrances.Keys.Except(oldEntrances.Keys).Order().ToList(),
            oldEntrances.Keys.Except(newEntrances.Keys).Order().ToList(),
            newEntrances.Keys.Intersect(oldEntrances.Keys).Where(id => oldEntrances[id] != newEntrances[id]).Order().ToList(),
            newFiles.Keys.Except(oldFiles.Keys).Order().ToList(),
            oldFiles.Keys.Except(newFiles.Keys).Order().ToList());

        void AddScalar(string name, object? oldValue, object? newValue)
        {
            if (!Equals(oldValue, newValue)) scalars[name] = (oldValue, newValue);
        }
    }

    public bool IsSemanticEqual(CavePublishedSnapshotV1 previous, CavePublishedSnapshotV1 current)
    {
        var diff = Compare(previous, current);
        return diff.Scalars.Count == 0 && diff.AddedTags.Count == 0 && diff.RemovedTags.Count == 0 &&
               diff.AddedEntrances.Count == 0 && diff.RemovedEntrances.Count == 0 && diff.ChangedEntrances.Count == 0 &&
               diff.AddedFiles.Count == 0 && diff.RemovedFiles.Count == 0;
    }
}
