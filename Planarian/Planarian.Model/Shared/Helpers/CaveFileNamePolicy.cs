namespace Planarian.Model.Shared.Helpers;

public static class CaveFileNamePolicy
{
    public static string GetEffectiveFileName(string existingFileName, string? existingDisplayName,
        string? proposedDisplayName) =>
        !string.IsNullOrWhiteSpace(proposedDisplayName) &&
        !string.Equals(proposedDisplayName, existingDisplayName, StringComparison.Ordinal)
            ? $"{proposedDisplayName}{Path.GetExtension(existingFileName)}"
            : existingFileName;
}
