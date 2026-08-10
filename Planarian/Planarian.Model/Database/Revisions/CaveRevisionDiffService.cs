namespace Planarian.Model.Database.Revisions;

/// <summary>
/// A descriptive value changing while its stable reference ID remains the same.
/// It is an effective historical change, not an association change and not an
/// assertion that the actor who published this Cave revision renamed the shared
/// reference entity.
/// </summary>
public sealed record ReferenceMetadataChange(string Path, string StableId, string Property,
    string? PreviousValue, string? CurrentValue);

public sealed record CaveRevisionDiff(
    IReadOnlyDictionary<string, (object? Previous, object? Current)> Scalars,
    IReadOnlyList<SnapshotTagReference> AddedTags,
    IReadOnlyList<SnapshotTagReference> RemovedTags,
    IReadOnlyList<string> AddedEntrances,
    IReadOnlyList<string> RemovedEntrances,
    IReadOnlyList<string> ChangedEntrances,
    IReadOnlyList<string> AddedFiles,
    IReadOnlyList<string> RemovedFiles,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<ReferenceMetadataChange> ReferenceMetadataChanges);

public sealed class CaveRevisionDiffService
{
    public CaveRevisionDiff Compare(CavePublishedSnapshotV1 previous, CavePublishedSnapshotV1 current)
    {
        var scalars = new Dictionary<string, (object?, object?)>();
        var metadata = new List<ReferenceMetadataChange>();
        AddScalar(nameof(CavePublishedSnapshotV1.Name), previous.Name, current.Name);
        AddScalar(nameof(CavePublishedSnapshotV1.AlternateNames), previous.AlternateNames, current.AlternateNames);
        CompareReference(nameof(CavePublishedSnapshotV1.State), previous.State, current.State,
            "NameAtRevision", "AbbreviationAtRevision");
        CompareReference(nameof(CavePublishedSnapshotV1.County), previous.County, current.County,
            "NameAtRevision", "DisplayIdAtRevision");
        AddScalar(nameof(CavePublishedSnapshotV1.CountyNumber), previous.CountyNumber, current.CountyNumber);
        AddScalar(nameof(CavePublishedSnapshotV1.ReportedByUserId), previous.ReportedByUserId, current.ReportedByUserId);
        AddScalar(nameof(CavePublishedSnapshotV1.LengthFeet), previous.LengthFeet, current.LengthFeet);
        AddScalar(nameof(CavePublishedSnapshotV1.DepthFeet), previous.DepthFeet, current.DepthFeet);
        AddScalar(nameof(CavePublishedSnapshotV1.MaxPitDepthFeet), previous.MaxPitDepthFeet, current.MaxPitDepthFeet);
        AddScalar(nameof(CavePublishedSnapshotV1.NumberOfPits), previous.NumberOfPits, current.NumberOfPits);
        AddScalar(nameof(CavePublishedSnapshotV1.Narrative), previous.Narrative, current.Narrative);
        AddScalar(nameof(CavePublishedSnapshotV1.ReportedOn), previous.ReportedOn, current.ReportedOn);
        AddScalar(nameof(CavePublishedSnapshotV1.IsArchived), previous.IsArchived, current.IsArchived);

        var oldTags = previous.Tags.ToDictionary(t => (t.Role, t.TagTypeId));
        var newTags = current.Tags.ToDictionary(t => (t.Role, t.TagTypeId));
        var addedTags = current.Tags.Where(t => !oldTags.ContainsKey((t.Role, t.TagTypeId))).OrderBy(t => t.Role).ThenBy(t => t.TagTypeId).ToList();
        var removedTags = previous.Tags.Where(t => !newTags.ContainsKey((t.Role, t.TagTypeId))).OrderBy(t => t.Role).ThenBy(t => t.TagTypeId).ToList();
        foreach (var key in oldTags.Keys.Intersect(newTags.Keys))
            AddTagMetadata($"Tags/{key.Role}", oldTags[key], newTags[key]);

        var oldEntrances = previous.Entrances.ToDictionary(e => e.Id);
        var newEntrances = current.Entrances.ToDictionary(e => e.Id);
        var changedEntrances = new List<string>();
        foreach (var id in oldEntrances.Keys.Intersect(newEntrances.Keys).Order())
        {
            var metadataBefore = metadata.Count;
            if (!EntranceSemanticEquals(oldEntrances[id], newEntrances[id], id))
                changedEntrances.Add(id);
            // The predicate intentionally records nested reference metadata. This
            // local is retained to make that side effect explicit to maintainers.
            _ = metadataBefore;
        }

        var oldFiles = previous.Files.ToDictionary(e => e.Id);
        var newFiles = current.Files.ToDictionary(e => e.Id);
        var changedFiles = new List<string>();
        foreach (var id in oldFiles.Keys.Intersect(newFiles.Keys).Order())
            if (!FileSemanticEquals(oldFiles[id], newFiles[id], id)) changedFiles.Add(id);

        return new CaveRevisionDiff(scalars, addedTags, removedTags,
            newEntrances.Keys.Except(oldEntrances.Keys).Order().ToList(),
            oldEntrances.Keys.Except(newEntrances.Keys).Order().ToList(), changedEntrances,
            newFiles.Keys.Except(oldFiles.Keys).Order().ToList(), oldFiles.Keys.Except(newFiles.Keys).Order().ToList(), changedFiles,
            metadata.OrderBy(change => change.Path).ThenBy(change => change.Property).ToList());

        void AddScalar(string name, object? oldValue, object? newValue)
        {
            if (!SemanticEquals(oldValue, newValue)) scalars[name] = (oldValue, newValue);
        }

        void CompareReference(string path, SnapshotReference oldReference, SnapshotReference newReference, params string[] metadataProperties)
        {
            if (oldReference.Id != newReference.Id)
            {
                AddScalar($"{path}.Id", oldReference.Id, newReference.Id);
                return;
            }

            foreach (var property in metadataProperties)
            {
                var oldValue = property switch
                {
                    "NameAtRevision" => oldReference.NameAtRevision,
                    "DisplayIdAtRevision" => oldReference.DisplayIdAtRevision,
                    "AbbreviationAtRevision" => oldReference.AbbreviationAtRevision,
                    _ => throw new ArgumentOutOfRangeException(nameof(property))
                };
                var newValue = property switch
                {
                    "NameAtRevision" => newReference.NameAtRevision,
                    "DisplayIdAtRevision" => newReference.DisplayIdAtRevision,
                    "AbbreviationAtRevision" => newReference.AbbreviationAtRevision,
                    _ => throw new ArgumentOutOfRangeException(nameof(property))
                };
                if (oldValue != newValue) metadata.Add(new ReferenceMetadataChange(path, oldReference.Id, property, oldValue, newValue));
            }
        }

        void AddTagMetadata(string path, SnapshotTagReference oldTag, SnapshotTagReference newTag)
        {
            if (oldTag.NameAtRevision != newTag.NameAtRevision)
                metadata.Add(new ReferenceMetadataChange(path, oldTag.TagTypeId, nameof(SnapshotTagReference.NameAtRevision), oldTag.NameAtRevision, newTag.NameAtRevision));
        }

        bool EntranceSemanticEquals(CaveEntranceSnapshotV1 oldEntrance, CaveEntranceSnapshotV1 newEntrance, string entranceId)
        {
            var equivalent = oldEntrance.Id == newEntrance.Id && oldEntrance.Name == newEntrance.Name &&
                             oldEntrance.IsPrimary == newEntrance.IsPrimary && oldEntrance.Description == newEntrance.Description &&
                             oldEntrance.ReportedByUserId == newEntrance.ReportedByUserId && oldEntrance.Latitude == newEntrance.Latitude &&
                             oldEntrance.Longitude == newEntrance.Longitude && oldEntrance.Elevation == newEntrance.Elevation &&
                             oldEntrance.Srid == newEntrance.Srid && oldEntrance.LocationQualityTagId == newEntrance.LocationQualityTagId &&
                             oldEntrance.ReportedOn == newEntrance.ReportedOn && oldEntrance.PitDepthFeet == newEntrance.PitDepthFeet;
            if (oldEntrance.LocationQualityTagId == newEntrance.LocationQualityTagId &&
                oldEntrance.LocationQualityNameAtRevision != newEntrance.LocationQualityNameAtRevision)
                metadata.Add(new ReferenceMetadataChange($"Entrances/{entranceId}/LocationQuality", oldEntrance.LocationQualityTagId,
                    nameof(CaveEntranceSnapshotV1.LocationQualityNameAtRevision), oldEntrance.LocationQualityNameAtRevision, newEntrance.LocationQualityNameAtRevision));

            var oldTags = oldEntrance.Tags.ToDictionary(t => (t.Role, t.TagTypeId));
            var newTags = newEntrance.Tags.ToDictionary(t => (t.Role, t.TagTypeId));
            foreach (var key in oldTags.Keys.Intersect(newTags.Keys))
                AddTagMetadata($"Entrances/{entranceId}/Tags/{key.Role}", oldTags[key], newTags[key]);
            return equivalent && oldTags.Keys.Order().SequenceEqual(newTags.Keys.Order()) &&
                   oldTags.All(pair => newTags.TryGetValue(pair.Key, out var tag) && pair.Value.NameAtRevision == tag.NameAtRevision) &&
                   oldEntrance.LocationQualityNameAtRevision == newEntrance.LocationQualityNameAtRevision;
        }

        bool FileSemanticEquals(CaveFileSnapshotV1 oldFile, CaveFileSnapshotV1 newFile, string fileId)
        {
            if (oldFile.FileTypeTagId == newFile.FileTypeTagId && oldFile.FileTypeNameAtRevision != newFile.FileTypeNameAtRevision)
                metadata.Add(new ReferenceMetadataChange($"Files/{fileId}/FileType", oldFile.FileTypeTagId,
                    nameof(CaveFileSnapshotV1.FileTypeNameAtRevision), oldFile.FileTypeNameAtRevision, newFile.FileTypeNameAtRevision));
            return oldFile.Id == newFile.Id && oldFile.FileTypeTagId == newFile.FileTypeTagId &&
                   oldFile.FileTypeNameAtRevision == newFile.FileTypeNameAtRevision && oldFile.FileName == newFile.FileName &&
                   oldFile.DisplayName == newFile.DisplayName;
        }

        static bool SemanticEquals(object? oldValue, object? newValue) =>
            oldValue is IEnumerable<string> oldStrings && newValue is IEnumerable<string> newStrings
                ? oldStrings.SequenceEqual(newStrings, StringComparer.Ordinal)
                : Equals(oldValue, newValue);
    }

    public bool IsSemanticEqual(CavePublishedSnapshotV1 previous, CavePublishedSnapshotV1 current)
    {
        var diff = Compare(previous, current);
        return diff.Scalars.Count == 0 && diff.AddedTags.Count == 0 && diff.RemovedTags.Count == 0 &&
               diff.AddedEntrances.Count == 0 && diff.RemovedEntrances.Count == 0 && diff.ChangedEntrances.Count == 0 &&
               diff.AddedFiles.Count == 0 && diff.RemovedFiles.Count == 0 && diff.ChangedFiles.Count == 0 &&
               diff.ReferenceMetadataChanges.Count == 0;
    }
}
