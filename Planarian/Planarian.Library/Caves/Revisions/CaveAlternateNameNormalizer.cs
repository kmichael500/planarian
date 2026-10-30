namespace Planarian.Modules.Caves.Revisions;

public static class CaveAlternateNameNormalizer
{
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? names) => (names ?? [])
        .Select(name => name?.Trim())
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Select(name => name!)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToList();
}
