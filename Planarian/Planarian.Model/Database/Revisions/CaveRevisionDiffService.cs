namespace Planarian.Model.Database.Revisions;

/// <summary>
/// A descriptive value changing while its stable reference ID remains the same.
/// It is an effective historical change, not an association change and not an
/// assertion that the actor who published this Cave revision renamed the shared
/// reference entity.
/// </summary>
public sealed record ReferenceMetadataChange(string Path, string StableId, string Property,
    string? PreviousValue, string? CurrentValue);

public sealed record CaveEntranceChange(
    string EntranceId,
    IReadOnlyDictionary<string, (object? Previous, object? Current)> Scalars,
    IReadOnlyList<SnapshotTagReference> AddedTags,
    IReadOnlyList<SnapshotTagReference> RemovedTags);

public sealed record CaveFileChange(
    string FileId,
    IReadOnlyDictionary<string, (object? Previous, object? Current)> Scalars);

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
    IReadOnlyList<CaveEntranceChange> EntranceChanges,
    IReadOnlyList<CaveFileChange> FileChanges,
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
        var entranceChanges = new List<CaveEntranceChange>();
        foreach (var id in oldEntrances.Keys.Intersect(newEntrances.Keys).Order())
        {
            var metadataBefore = metadata.Count;
            var change = CompareEntrance(oldEntrances[id], newEntrances[id], id);
            if (change.Scalars.Count != 0 || change.AddedTags.Count != 0 || change.RemovedTags.Count != 0 ||
                metadata.Count != metadataBefore)
                entranceChanges.Add(change);
        }

        var oldFiles = previous.Files.ToDictionary(e => e.Id);
        var newFiles = current.Files.ToDictionary(e => e.Id);
        var fileChanges = new List<CaveFileChange>();
        foreach (var id in oldFiles.Keys.Intersect(newFiles.Keys).Order())
        {
            var metadataBefore = metadata.Count;
            var change = CompareFile(oldFiles[id], newFiles[id], id);
            if (change.Scalars.Count != 0 || metadata.Count != metadataBefore)
                fileChanges.Add(change);
        }

        return new CaveRevisionDiff(scalars, addedTags, removedTags,
            newEntrances.Keys.Except(oldEntrances.Keys).Order().ToList(),
            oldEntrances.Keys.Except(newEntrances.Keys).Order().ToList(), entranceChanges.Select(change => change.EntranceId).ToList(),
            newFiles.Keys.Except(oldFiles.Keys).Order().ToList(), oldFiles.Keys.Except(newFiles.Keys).Order().ToList(), fileChanges.Select(change => change.FileId).ToList(),
            entranceChanges, fileChanges,
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

        CaveEntranceChange CompareEntrance(CaveEntranceSnapshotV1 oldEntrance, CaveEntranceSnapshotV1 newEntrance, string entranceId)
        {
            var nestedScalars = new Dictionary<string, (object?, object?)>();
            AddNestedScalar(nameof(CaveEntranceSnapshotV1.Name), oldEntrance.Name, newEntrance.Name);
            AddNestedScalar(nameof(CaveEntranceSnapshotV1.IsPrimary), oldEntrance.IsPrimary, newEntrance.IsPrimary);
            AddNestedScalar(nameof(CaveEntranceSnapshotV1.Description), oldEntrance.Description, newEntrance.Description);
            AddNestedScalar(nameof(CaveEntranceSnapshotV1.ReportedByUserId), oldEntrance.ReportedByUserId, newEntrance.ReportedByUserId);
            AddNestedScalar(nameof(CaveEntranceSnapshotV1.Latitude), oldEntrance.Latitude, newEntrance.Latitude);
            AddNestedScalar(nameof(CaveEntranceSnapshotV1.Longitude), oldEntrance.Longitude, newEntrance.Longitude);
            AddNestedScalar(nameof(CaveEntranceSnapshotV1.Elevation), oldEntrance.Elevation, newEntrance.Elevation);
            AddNestedScalar(nameof(CaveEntranceSnapshotV1.Srid), oldEntrance.Srid, newEntrance.Srid);
            AddNestedScalar(nameof(CaveEntranceSnapshotV1.LocationQualityTagId), oldEntrance.LocationQualityTagId, newEntrance.LocationQualityTagId);
            AddNestedScalar(nameof(CaveEntranceSnapshotV1.ReportedOn), oldEntrance.ReportedOn, newEntrance.ReportedOn);
            AddNestedScalar(nameof(CaveEntranceSnapshotV1.PitDepthFeet), oldEntrance.PitDepthFeet, newEntrance.PitDepthFeet);

            if (oldEntrance.LocationQualityTagId == newEntrance.LocationQualityTagId &&
                oldEntrance.LocationQualityNameAtRevision != newEntrance.LocationQualityNameAtRevision)
                metadata.Add(new ReferenceMetadataChange($"Entrances/{entranceId}/LocationQuality", oldEntrance.LocationQualityTagId,
                    nameof(CaveEntranceSnapshotV1.LocationQualityNameAtRevision), oldEntrance.LocationQualityNameAtRevision, newEntrance.LocationQualityNameAtRevision));

            var oldTags = oldEntrance.Tags.ToDictionary(t => (t.Role, t.TagTypeId));
            var newTags = newEntrance.Tags.ToDictionary(t => (t.Role, t.TagTypeId));
            foreach (var key in oldTags.Keys.Intersect(newTags.Keys))
                AddTagMetadata($"Entrances/{entranceId}/Tags/{key.Role}", oldTags[key], newTags[key]);

            return new CaveEntranceChange(entranceId, nestedScalars,
                newEntrance.Tags.Where(tag => !oldTags.ContainsKey((tag.Role, tag.TagTypeId))).OrderBy(tag => tag.Role).ThenBy(tag => tag.TagTypeId).ToList(),
                oldEntrance.Tags.Where(tag => !newTags.ContainsKey((tag.Role, tag.TagTypeId))).OrderBy(tag => tag.Role).ThenBy(tag => tag.TagTypeId).ToList());

            void AddNestedScalar(string name, object? oldValue, object? newValue)
            {
                if (!SemanticEquals(oldValue, newValue)) nestedScalars[name] = (oldValue, newValue);
            }
        }

        CaveFileChange CompareFile(CaveFileSnapshotV1 oldFile, CaveFileSnapshotV1 newFile, string fileId)
        {
            var nestedScalars = new Dictionary<string, (object?, object?)>();
            AddNestedScalar(nameof(CaveFileSnapshotV1.FileTypeTagId), oldFile.FileTypeTagId, newFile.FileTypeTagId);
            AddNestedScalar(nameof(CaveFileSnapshotV1.FileName), oldFile.FileName, newFile.FileName);
            AddNestedScalar(nameof(CaveFileSnapshotV1.DisplayName), oldFile.DisplayName, newFile.DisplayName);
            if (oldFile.FileTypeTagId == newFile.FileTypeTagId && oldFile.FileTypeNameAtRevision != newFile.FileTypeNameAtRevision)
                metadata.Add(new ReferenceMetadataChange($"Files/{fileId}/FileType", oldFile.FileTypeTagId,
                    nameof(CaveFileSnapshotV1.FileTypeNameAtRevision), oldFile.FileTypeNameAtRevision, newFile.FileTypeNameAtRevision));
            return new CaveFileChange(fileId, nestedScalars);

            void AddNestedScalar(string name, object? oldValue, object? newValue)
            {
                if (!SemanticEquals(oldValue, newValue)) nestedScalars[name] = (oldValue, newValue);
            }
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
