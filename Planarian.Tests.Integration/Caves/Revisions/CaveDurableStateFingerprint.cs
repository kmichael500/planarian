using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;
using Xunit;

namespace Planarian.Tests.Integration.Caves.Revisions;

/// <summary>
/// Observes persisted domain rows using a fresh connection: EF's change tracker, projection
/// materialization, and uncommitted state cannot make a read-only assertion pass accidentally.
/// Infrastructure access counters and request diagnostics are intentionally not part of this contract.
/// </summary>
internal static class CaveDurableStateFingerprint
{
    // Cave-domain entities share the RidgeWalker CLR namespace. The shared Tag entities
    // live outside it but are part of the same read/write integrity boundary.
    // Select entity types, not physical table names: EF metadata owns the mapping.
    private static bool IsDomainEntity(IEntityType entity) =>
        entity.ClrType.Namespace == typeof(Cave).Namespace ||
        entity.ClrType == typeof(TagType) ||
        entity.ClrType == typeof(LeadTag) ||
        entity.ClrType == typeof(TripTag);

    internal static async Task<IReadOnlyDictionary<string, string>> CaptureAsync(PlanarianDbContext db)
    {
        var tables = db.Model.GetEntityTypes()
            .Where(IsDomainEntity)
            .Select(entity => entity.GetTableName())
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Guard the integrity-critical types against being silently dropped by a refactor.
        foreach (var clrType in new[]
                 {
                     typeof(Cave), typeof(CaveRevision), typeof(CaveChangeRequest),
                     typeof(CaveProposalVersion), typeof(CaveGeoJson), typeof(Entrance),
                     typeof(File), typeof(StorageCleanupTarget), typeof(County),
                     typeof(State), typeof(Favorite), typeof(TagType)
                 })
        {
            var mappedTable = db.Model.FindEntityType(clrType)?.GetTableName();
            Assert.False(string.IsNullOrWhiteSpace(mappedTable), $"{clrType.Name} must map to a table.");
            Assert.Contains(mappedTable!, tables);
        }

        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            // Table identifiers originate only in the trusted EF model, never in request input.
            // Include xmin so even a write that re-saves identical business column values is detected.
            var identifier = table.Replace("\"", "\"\"", StringComparison.Ordinal);
            await using var command = new NpgsqlCommand(
                $"SELECT md5(COALESCE(string_agg(to_jsonb(t)::text || ':' || t.xmin::text, '\\n' ORDER BY to_jsonb(t)::text, t.xmin::text), '')) FROM \"{identifier}\" t",
                connection);
            hashes.Add(table, (string)(await command.ExecuteScalarAsync())!);
        }
        return hashes;
    }

    internal static async Task AssertUnchangedAsync(PlanarianDbContext db,
        IReadOnlyDictionary<string, string> before, string operation = "Read or rejected mutation")
    {
        var after = await CaptureAsync(db);
        Assert.Equal(before.Keys, after.Keys);
        foreach (var (table, hash) in before)
            Assert.True(hash == after[table], $"{operation} changed persisted {table} rows.");
    }
}
