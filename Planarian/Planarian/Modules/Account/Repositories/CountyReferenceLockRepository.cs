using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Repositories;

public sealed record LockedCounty(string Id, string AccountId, string StateId, string DisplayId, string Name);

/// <summary>
/// Coordinates County reference writers with administrative County mutations.
/// The caller owns the transaction; locks remain held until that transaction completes.
/// </summary>
public sealed class CountyReferenceLockRepository
{
    private const int BatchSize = 500;
    private readonly PlanarianDbContext _db;
    private readonly string _accountId;

    public CountyReferenceLockRepository(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _accountId = requestUser.AccountId
            ?? throw new InvalidOperationException("A current account is required for County locking.");
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
            var rows = await _db.Counties.FromSqlRaw("""
                    select *
                    from "Counties"
                    where "AccountId" = @account_id and "Id" = any(@county_ids)
                    order by "Id" collate "C"
                    for key share
                    """,
                    new NpgsqlParameter<string>("account_id", _accountId),
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
            var rows = await _db.Counties.FromSqlRaw("""
                    select *
                    from "Counties"
                    where "AccountId" = @account_id and "Id" = any(@county_ids)
                    order by "Id" collate "C"
                    for update
                    """,
                    new NpgsqlParameter<string>("account_id", _accountId),
                    new NpgsqlParameter<string[]>("county_ids", chunk))
                .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
            result.AddRange(rows.Select(ToLockedCounty));
        }

        return result;
    }

    private void RequireTransaction()
    {
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("County locks require an active PostgreSQL transaction.");
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> countyIds) =>
        countyIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    private static LockedCounty ToLockedCounty(Planarian.Model.Database.Entities.RidgeWalker.County county) =>
        new(county.Id, county.AccountId, county.StateId, county.DisplayId, county.Name);
}
