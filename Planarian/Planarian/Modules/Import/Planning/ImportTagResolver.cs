using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;

namespace Planarian.Modules.Import.Planning;

internal static class ImportTagResolver
{
    public static async Task<ImportTagResolutionSet> ResolveAsync(PlanarianDbContext db, string accountId,
        IEnumerable<(string Key, IEnumerable<string?> Names)> requests,
        CancellationToken cancellationToken)
    {
        var normalized = new List<(string Key, string Name)>();
        var seen = new HashSet<TagIdentity>(TagIdentityComparer.Instance);
        foreach (var (key, names) in requests)
        foreach (var value in names)
        {
            var name = value?.Trim();
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(new TagIdentity(key, name))) continue;
            normalized.Add((key, name));
        }

        var keys = normalized.Select(request => request.Key).Distinct(StringComparer.Ordinal).ToList();
        var existing = await db.TagTypes
            .Where(tag => keys.Contains(tag.Key) && (tag.AccountId == accountId || tag.IsDefault))
            .AsNoTracking()
            .Select(tag => new ImportTagLookup(tag.Id, tag.Key, tag.Name, tag.AccountId, tag.IsDefault, false))
            .ToListAsync(cancellationToken);

        var selected = new Dictionary<TagIdentity, ImportTagLookup>(TagIdentityComparer.Instance);
        var creations = new List<ImportTagCreationIntent>();
        foreach (var (key, name) in normalized)
        {
            var match = existing
                .Where(tag => tag.Key == key && tag.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                .OrderByDescending(tag => tag.Name.Equals(name, StringComparison.Ordinal))
                .ThenByDescending(tag => tag.AccountId == accountId)
                .ThenBy(tag => tag.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (match is null)
            {
                if (name.Length > PropertyLength.Name)
                    throw ApiExceptionDictionary.BadRequest(
                        $"Tag '{name}' exceeds the maximum allowed length of {PropertyLength.Name}");
                var creation = new ImportTagCreationIntent(IdGenerator.Generate(), key, name);
                creations.Add(creation);
                match = new ImportTagLookup(creation.Id, key, name, accountId, false, true);
                existing.Add(match);
            }
            selected[new TagIdentity(key, name)] = match;
        }

        return new ImportTagResolutionSet(selected, existing, creations);
    }

    public static ImportTagLookup? Resolve(ImportTagResolutionSet set, string key, string? value)
    {
        var name = value?.Trim();
        return string.IsNullOrWhiteSpace(name)
            ? null
            : set.Selected.GetValueOrDefault(new TagIdentity(key, name));
    }

    public static List<ImportTagLookup> ResolveMany(ImportTagResolutionSet set, string key,
        IEnumerable<string?> values)
    {
        var result = new List<ImportTagLookup>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var tag = Resolve(set, key, value) ?? throw ApiExceptionDictionary.NotFound(key);
            if (seenIds.Add(tag.Id)) result.Add(tag);
        }
        return result;
    }

    internal sealed record TagIdentity(string Key, string Name);

    internal sealed class TagIdentityComparer : IEqualityComparer<TagIdentity>
    {
        public static readonly TagIdentityComparer Instance = new();

        public bool Equals(TagIdentity? x, TagIdentity? y) =>
            x is not null && y is not null &&
            StringComparer.Ordinal.Equals(x.Key, y.Key) &&
            StringComparer.InvariantCultureIgnoreCase.Equals(x.Name, y.Name);

        public int GetHashCode(TagIdentity obj) => HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(obj.Key),
            StringComparer.InvariantCultureIgnoreCase.GetHashCode(obj.Name));
    }
}

internal sealed record ImportTagLookup(
    string Id, string Key, string Name, string? AccountId, bool IsDefault, bool IsCreation);

internal sealed record ImportTagResolutionSet(
    IReadOnlyDictionary<ImportTagResolver.TagIdentity, ImportTagLookup> Selected,
    IReadOnlyList<ImportTagLookup> All,
    IReadOnlyList<ImportTagCreationIntent> Creations);
