using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Repositories;

/// <summary>Executes account County deletion under the shared deterministic mutation-lock protocol.</summary>
public sealed class CountyDeleteExecutionRepository
{
    private const int BatchSize = 500;
    private readonly PlanarianDbContext _db;
    private readonly string _accountId;
    private readonly CountyReferenceLockRepository _countyLocks;

    public CountyDeleteExecutionRepository(PlanarianDbContext db, RequestUser requestUser,
        CountyReferenceLockRepository countyLocks)
    {
        _db = db;
        _accountId = requestUser.AccountId
            ?? throw new InvalidOperationException("A current account is required for County deletion.");
        _countyLocks = countyLocks;
    }

    public async Task<int> ExecuteAsync(IEnumerable<string> countyIds,
        CancellationToken cancellationToken = default)
    {
        var ids = countyIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return 0;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var locked = await _countyLocks.LockForMutationAsync(ids, cancellationToken);
            if (locked.Count != ids.Count) throw ApiExceptionDictionary.NotFound("County Id");

            var deleted = 0;
            try
            {
                foreach (var chunk in ids.Chunk(BatchSize))
                    deleted += await _db.Counties.IgnoreQueryFilters()
                        .Where(county => county.AccountId == _accountId && chunk.Contains(county.Id))
                        .ExecuteDeleteAsync(cancellationToken);
            }
            catch (PostgresException exception)
                when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                throw ApiExceptionDictionary.BadRequest("Cannot delete county because it is in use.");
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
