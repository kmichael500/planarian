using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Persistence;

public sealed record LockedCounty(string Id, string AccountId, string StateId, string DisplayId, string Name);

/// <summary>
/// Coordinates County reference writers with administrative County mutations.
/// The caller owns the transaction; locks remain held until that transaction completes.
/// </summary>
public sealed class CountyReferenceLocker : AccountScopedDataAccessBase
{
    private const int BatchSize = 500;

    public CountyReferenceLocker(PlanarianDbContext db, RequestUser requestUser) : base(db, requestUser)
    {
    }

    /// <summary>
    /// Serializes County-code administration for the current account without locking the Account row itself.
    /// Cave inserts already take County locks before PostgreSQL acquires Account FK key-share locks, so using
    /// the Account row as this mutex would invert that ordering and can deadlock. A transaction-scoped advisory
    /// lock provides the same account-local serialization without joining the relational FK lock graph.
    /// </summary>
    public async Task LockAdministrationNamespaceAsync(CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await DbContext.Database.ExecuteSqlRawAsync(
            "select pg_advisory_xact_lock(1885434478, hashtext(@account_id))",
            [new NpgsqlParameter<string>("account_id", AccountId)], cancellationToken);
    }

    public Task<IReadOnlyList<LockedCounty>> LockForReferenceAsync(IEnumerable<string> countyIds,
        CancellationToken cancellationToken = default) =>
        LockForReferenceCoreAsync(Normalize(countyIds), cancellationToken);

    public Task<IReadOnlyList<LockedCounty>> LockForMutationAsync(IEnumerable<string> countyIds,
        CancellationToken cancellationToken = default) =>
        LockForMutationCoreAsync(Normalize(countyIds), cancellationToken);

    private async Task<IReadOnlyList<LockedCounty>> LockForReferenceCoreAsync(IReadOnlyList<string> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return [];
        RequireTransaction();

        var result = new List<LockedCounty>(ids.Count);
        foreach (var chunk in ids.Chunk(BatchSize))
        {
            var rows = await DbContext.Counties.FromSqlRaw("""
                    select *
                    from "Counties"
                    where "AccountId" = @account_id and "Id" = any(@county_ids)
                    order by "Id" collate "C"
                    for key share
                    """,
                    new NpgsqlParameter<string>("account_id", AccountId),
                    new NpgsqlParameter<string[]>("county_ids", chunk))
                .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
            result.AddRange(rows.Select(ToLockedCounty));
        }

        return result;
    }

    private async Task<IReadOnlyList<LockedCounty>> LockForMutationCoreAsync(IReadOnlyList<string> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return [];
        RequireTransaction();

        var result = new List<LockedCounty>(ids.Count);
        foreach (var chunk in ids.Chunk(BatchSize))
        {
            var rows = await DbContext.Counties.FromSqlRaw("""
                    select *
                    from "Counties"
                    where "AccountId" = @account_id and "Id" = any(@county_ids)
                    order by "Id" collate "C"
                    for update
                    """,
                    new NpgsqlParameter<string>("account_id", AccountId),
                    new NpgsqlParameter<string[]>("county_ids", chunk))
                .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
            result.AddRange(rows.Select(ToLockedCounty));
        }

        return result;
    }

    private void RequireTransaction()
    {
        if (DbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("County locks require an active PostgreSQL transaction.");
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> countyIds) =>
        countyIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    private static LockedCounty ToLockedCounty(Planarian.Model.Database.Entities.RidgeWalker.County county) =>
        new(county.Id, county.AccountId, county.StateId, county.DisplayId, county.Name);
}
