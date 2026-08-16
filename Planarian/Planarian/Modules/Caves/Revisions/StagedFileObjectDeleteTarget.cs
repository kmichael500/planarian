namespace Planarian.Modules.Caves.Revisions;

public sealed record StagedFileObjectDeleteTarget(
    string StorageKey,
    string StoragePartition);
