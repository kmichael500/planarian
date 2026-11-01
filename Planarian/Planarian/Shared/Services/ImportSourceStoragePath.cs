namespace Planarian.Shared.Services;

public static class ImportSourceStoragePath
{
    public const string CaveEntranceCsvPrefix = "temp/import/caves/";

    public static string BuildCaveEntranceCsvKey(string fileId) =>
        CaveEntranceCsvPrefix + fileId;

    public static bool IsCaveEntranceCsvKey(string? storageKey) =>
        storageKey?.StartsWith(CaveEntranceCsvPrefix, StringComparison.Ordinal) == true;
}
