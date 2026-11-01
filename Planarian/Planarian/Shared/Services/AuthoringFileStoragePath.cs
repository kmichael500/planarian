namespace Planarian.Shared.Services;

public static class AuthoringFileStoragePath
{
    public const string Prefix = "objects/files/";

    public static string BuildKey(string fileId) => Prefix + fileId;

    public static bool IsKeyForFile(string? storageKey, string fileId) =>
        string.Equals(storageKey, BuildKey(fileId), StringComparison.Ordinal);
}
