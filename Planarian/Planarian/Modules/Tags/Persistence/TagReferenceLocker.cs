using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Tags.Persistence;

public sealed record LockedTagType(string Id, string Name, string Key, string? AccountId, bool IsDefault);

/// <summary>
/// Coordinates published Cave reference writers with destructive TagType mutations.
/// The caller owns the transaction; locks remain held until that transaction completes.
/// </summary>
public sealed class TagReferenceLocker : AccountScopedDataAccessBase
{
    private const int BatchSize = 500;

    public TagReferenceLocker(PlanarianDbContext db, RequestUser requestUser) : base(db, requestUser)
    {
    }

    public Task<IReadOnlyList<LockedTagType>> LockForReferenceAsync(IEnumerable<string> tagTypeIds,
        CancellationToken cancellationToken = default) =>
        LockAsync(tagTypeIds, "key share", cancellationToken);

    public Task<IReadOnlyList<LockedTagType>> LockForDestructiveMutationAsync(IEnumerable<string> tagTypeIds,
        CancellationToken cancellationToken = default) =>
        LockAsync(tagTypeIds, "update", cancellationToken);

    private async Task<IReadOnlyList<LockedTagType>> LockAsync(IEnumerable<string> tagTypeIds, string lockMode,
        CancellationToken cancellationToken)
    {
        var ids = tagTypeIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return [];

        if (DbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("TagType locks require an active PostgreSQL transaction.");
        var result = new List<LockedTagType>(ids.Count);
        foreach (var chunk in ids.Chunk(BatchSize))
        {
            var idParameter = new NpgsqlParameter<string[]>("tag_type_ids", chunk);
            var accountParameter = new NpgsqlParameter<string>("account_id", AccountId);
            var sql = lockMode == "key share" ? """
                select *
                from "TagTypes"
                where "Id" = any(@tag_type_ids)
                  and ("AccountId" = @account_id or "IsDefault")
                order by "Id" collate "C"
                for key share
                """ : """
                select *
                from "TagTypes"
                where "Id" = any(@tag_type_ids)
                  and ("AccountId" = @account_id or "IsDefault")
                order by "Id" collate "C"
                for update
                """;
            var rows = await DbContext.TagTypes.FromSqlRaw(sql, idParameter, accountParameter)
                .IgnoreQueryFilters().AsNoTracking()
                .ToListAsync(cancellationToken);
            result.AddRange(rows.Select(row =>
                new LockedTagType(row.Id, row.Name, row.Key, row.AccountId, row.IsDefault)));
        }

        return result;
    }
}
