namespace Planarian.Modules.Account.Model;

internal static class AccountBackupArchivePaths
{
    private static readonly HashSet<char> InvalidPathSegmentCharacters = Enumerable.Range(0, 32)
        .Append(127)
        .Select(value => (char)value)
        .Concat(['"', '*', '/', ':', '<', '>', '?', '\\', '|'])
        .ToHashSet();

    private static readonly HashSet<string> WindowsReservedPathSegmentNames =
    [
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9"
    ];

    public static string BuildCaveFolder(AccountBackupCaveDto cave)
    {
        var stateFolder = SanitizePathSegment(cave.State, "Unknown State");
        var countyFolder = SanitizePathSegment($"{cave.CountyCode} {cave.CountyName}", cave.CountyCode);
        var caveDisplayId = $"{cave.CountyCode}{cave.CountyIdDelimiter}{cave.CountyCaveNumber}";
        var caveFolder = SanitizePathSegment($"{caveDisplayId} {cave.CaveName}", caveDisplayId);

        return $"{stateFolder}/{countyFolder}/{caveFolder}";
    }

    public static string CreateUniqueEntryPath(string desiredPath, ISet<string> existingPaths)
    {
        if (existingPaths.Add(desiredPath))
        {
            return desiredPath;
        }

        var extension = Path.GetExtension(desiredPath);
        var directory = Path.GetDirectoryName(desiredPath)?.Replace('\\', '/');
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(desiredPath);
        var suffix = 2;

        while (true)
        {
            var candidateFileName = $"{fileNameWithoutExtension} ({suffix}){extension}";
            var candidatePath = string.IsNullOrWhiteSpace(directory)
                ? candidateFileName
                : $"{directory}/{candidateFileName}";

            if (existingPaths.Add(candidatePath))
            {
                return candidatePath;
            }

            suffix++;
        }
    }

    public static string EnsureGeoJsonFileName(string? fileName)
    {
        var normalizedFileName = string.IsNullOrWhiteSpace(fileName) ? "Line Plot" : fileName.Trim();
        return Path.HasExtension(normalizedFileName)
            ? normalizedFileName
            : $"{normalizedFileName}.geojson";
    }

    public static string SanitizeFileName(string? value, string fallback)
    {
        return SanitizePathSegment(value, fallback);
    }

    public static string SanitizePathSegment(string? value, string fallback)
    {
        var sanitized = SanitizePathSegmentValue(value, fallback);
        if (IsSafePathSegment(sanitized))
        {
            return sanitized;
        }

        var sanitizedFallback = SanitizePathSegmentValue(fallback, fallback);
        return IsSafePathSegment(sanitizedFallback) ? sanitizedFallback : "item";
    }

    private static string SanitizePathSegmentValue(string? value, string fallback)
    {
        var workingValue = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var sanitizedCharacters = workingValue
            .Select(character => InvalidPathSegmentCharacters.Contains(character) ? ' ' : character)
            .ToArray();
        var sanitized = string.Join(
            " ",
            new string(sanitizedCharacters).Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return sanitized.TrimEnd(' ', '.');
    }

    private static bool IsSafePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value is "." or "..")
        {
            return false;
        }

        var extensionSeparatorIndex = value.IndexOf('.');
        var deviceNameCandidate = extensionSeparatorIndex >= 0
            ? value[..extensionSeparatorIndex]
            : value;

        return !WindowsReservedPathSegmentNames.Contains(deviceNameCandidate);
    }
}