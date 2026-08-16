using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Repositories;

/// <summary>
/// Executes account TagType deletion under the shared deterministic destructive-lock protocol.
/// </summary>
public sealed class TagTypeDeleteExecutionRepository
{
    private const int BatchSize = 100;
    private readonly PlanarianDbContext _db;
    private readonly AccountExecutionScope _scope;

    public TagTypeDeleteExecutionRepository(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
    }

    public async Task<int> ExecuteAsync(IEnumerable<string> tagTypeIds,
        CancellationToken cancellationToken = default)
    {
        var ids = tagTypeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return 0;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var chunk in ids.Chunk(BatchSize))
                await _db.TagTypes.FromSqlInterpolated($"""
                        select * from "TagTypes"
                        where "AccountId" = {_scope.AccountId} and "Id" = any({chunk})
                        order by "Id" collate "C" for update
                        """)
                    .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);

            var deleted = 0;
            try
            {
                foreach (var chunk in ids.Chunk(BatchSize))
                    deleted += await _db.TagTypes.IgnoreQueryFilters()
                        .Where(tag => tag.AccountId == _scope.AccountId && chunk.Contains(tag.Id))
                        .ExecuteDeleteAsync(cancellationToken);
            }
            catch (PostgresException exception)
                when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                throw ApiExceptionDictionary.BadRequest("Cannot delete tag type because it is in use.");
            }

            await transaction.CommitAsync(cancellationToken);
            return deleted;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
