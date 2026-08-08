using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Model.Interceptors;

/// <summary>
/// Resolves Cave/Entrance ownership once per distinct foreign-key chunk before
/// SaveChanges validation. This keeps tenant validation set-oriented instead of
/// issuing one SELECT per association row.
/// </summary>
internal static class SaveChangesOwnershipBatchLoader
{
    private const int LookupBatchSize = 500;

    public static async Task PopulateAsync(PlanarianDbContextBase context, IReadOnlyList<EntityEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var changed = entries.Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (changed.Count == 0) return;

        var caveLinks = new List<(object Entity, PropertyInfo Navigation, string CaveId)>();
        var entranceLinks = new List<(object Entity, PropertyInfo Navigation, string EntranceId)>();

        foreach (var entry in changed)
        {
            var entity = entry.Entity;
            var type = entity.GetType();

            var caveIdProperty = type.GetProperty("CaveId", BindingFlags.Public | BindingFlags.Instance);
            var caveNavigation = type.GetProperty("Cave", BindingFlags.Public | BindingFlags.Instance);
            if (caveIdProperty?.PropertyType == typeof(string) && caveNavigation?.PropertyType == typeof(Cave) &&
                caveNavigation.CanWrite && caveNavigation.GetValue(entity) is null &&
                caveIdProperty.GetValue(entity) is string caveId && !string.IsNullOrWhiteSpace(caveId))
            {
                caveLinks.Add((entity, caveNavigation, caveId));
            }

            var entranceIdProperty = type.GetProperty("EntranceId", BindingFlags.Public | BindingFlags.Instance);
            var entranceNavigation = type.GetProperty("Entrance", BindingFlags.Public | BindingFlags.Instance);
            if (entranceIdProperty?.PropertyType == typeof(string) && entranceNavigation?.PropertyType == typeof(Entrance) &&
                entranceNavigation.CanWrite && entranceNavigation.GetValue(entity) is null &&
                entranceIdProperty.GetValue(entity) is string entranceId && !string.IsNullOrWhiteSpace(entranceId))
            {
                entranceLinks.Add((entity, entranceNavigation, entranceId));
            }
        }

        if (caveLinks.Count == 0 && entranceLinks.Count == 0) return;

        var accountId = context.RequestUser?.AccountId;
        if (string.IsNullOrWhiteSpace(accountId))
            throw ApiExceptionDictionary.Forbidden("You do not have permission to modify this entity.");

        var caves = entries.Select(e => e.Entity).OfType<Cave>()
            .Where(c => c.AccountId == accountId && !string.IsNullOrWhiteSpace(c.Id))
            .GroupBy(c => c.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        if (caveLinks.Count > 0)
        {
            var unresolvedCaveIds = caveLinks.Select(link => link.CaveId)
                .Distinct(StringComparer.Ordinal)
                .Where(id => !caves.ContainsKey(id))
                .ToList();
            foreach (var chunk in unresolvedCaveIds.Chunk(LookupBatchSize))
            {
                var rows = await context.Caves.IgnoreQueryFilters()
                    .Where(c => c.AccountId == accountId && chunk.Contains(c.Id))
                    .ToListAsync(cancellationToken);
                foreach (var cave in rows) caves[cave.Id] = cave;
            }

            foreach (var link in caveLinks)
            {
                if (!caves.TryGetValue(link.CaveId, out var cave))
                    throw ApiExceptionDictionary.Forbidden("You do not have permission to modify this entity.");
                link.Navigation.SetValue(link.Entity, cave);
            }
        }

        var entrances = entries.Select(e => e.Entity).OfType<Entrance>()
            .Where(e => !string.IsNullOrWhiteSpace(e.Id) && e.Cave is not null && e.Cave.AccountId == accountId)
            .GroupBy(e => e.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        if (entranceLinks.Count > 0)
        {
            var unresolvedEntranceIds = entranceLinks.Select(link => link.EntranceId)
                .Distinct(StringComparer.Ordinal)
                .Where(id => !entrances.ContainsKey(id))
                .ToList();
            foreach (var chunk in unresolvedEntranceIds.Chunk(LookupBatchSize))
            {
                var rows = await context.Entrances.IgnoreQueryFilters()
                    .Where(e => chunk.Contains(e.Id) && e.Cave != null && e.Cave.AccountId == accountId)
                    .Include(e => e.Cave)
                    .ToListAsync(cancellationToken);
                foreach (var entrance in rows) entrances[entrance.Id] = entrance;
            }

            foreach (var link in entranceLinks)
            {
                if (!entrances.TryGetValue(link.EntranceId, out var entrance))
                    throw ApiExceptionDictionary.Forbidden("You do not have permission to modify this entity.");
                link.Navigation.SetValue(link.Entity, entrance);
            }
        }
    }
}
