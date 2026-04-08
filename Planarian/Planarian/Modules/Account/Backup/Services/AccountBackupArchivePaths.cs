namespace Planarian.Modules.Account.Backup.Services;

public static class AccountBackupArchivePaths
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    public static string SanitizePathSegment(string? name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        var sanitized = string.Concat(name.Where(c => !InvalidChars.Contains(c))).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }
}
