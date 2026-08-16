namespace Planarian.Modules.Caves.Revisions;

/// <summary>
/// Identifies an already-uploaded immutable staged File object that may be
/// associated with a Cave during publication. Publication never copies bytes.
/// </summary>
public sealed record StagedCaveFilePublication(
    string FileId,
    string StorageKey,
    string StoragePartition);
