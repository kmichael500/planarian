using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Caves.Revisions;
using Planarian.Shared.Base;

namespace Planarian.Modules.Account.Repositories;

public sealed record CountyMutationResult(string Id, string Name, string DisplayId);

/// <summary>
/// Owns County create/update persistence, validation, locking, and transaction details.
/// Application services provide validated user intent and map the result for presentation.
/// </summary>
public sealed class CountyAdministrationRepository : RepositoryBase
{
    private const int BatchSize = 500;
    private readonly AccountExecutionScope _scope;
    private readonly CountyReferenceLockRepository _countyLocks;
    private readonly CavePublishedSnapshotRepository _snapshots;
    private readonly CaveBulkRevisionRepository _revisions;

    public CountyAdministrationRepository(PlanarianDbContext db, RequestUser requestUser,
        CountyReferenceLockRepository countyLocks, CavePublishedSnapshotRepository snapshots,
        CaveBulkRevisionRepository revisions) : base(db, requestUser)
    {
        _scope = AccountExecutionScope.Require(requestUser);
        _countyLocks = countyLocks;
        _snapshots = snapshots;
        _revisions = revisions;
    }

    public async Task<IEnumerable<TagTypeTableCountyVm>> GetCountiesForTable(string stateId,
        CancellationToken cancellationToken)
    {
        var counties = await DbContext.Counties.AsNoTracking()
            .Where(county => county.AccountId == _scope.AccountId && county.StateId == stateId)
            .Select(county => new { county.Id, county.DisplayId, county.Name })
            .OrderBy(county => county.Name)
            .ToListAsync(cancellationToken);
        var occurrences = await DbContext.Caves.IgnoreQueryFilters().AsNoTracking()
            .Where(cave => cave.AccountId == _scope.AccountId && cave.StateId == stateId)
            .GroupBy(cave => cave.CountyId)
            .Select(group => new { CountyId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.CountyId, group => group.Count, StringComparer.Ordinal,
                cancellationToken);

        return counties.Select(county =>
        {
            var count = occurrences.GetValueOrDefault(county.Id);
            return new TagTypeTableCountyVm
            {
                TagTypeId = county.Id,
                CountyDisplayId = county.DisplayId,
                Name = county.Name,
                IsUserModifiable = count == 0,
                Occurrences = count
            };
        }).ToList();
    }

    public async Task MergeAsync(string stateId, IEnumerable<string> sourceCountyIds,
        string destinationCountyId, CancellationToken cancellationToken = default)
    {
        stateId = stateId?.Trim() ?? string.Empty;
        destinationCountyId = destinationCountyId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(stateId))
            throw ApiExceptionDictionary.BadRequest("A State is required for County merge.");
        if (string.IsNullOrWhiteSpace(destinationCountyId))
            throw ApiExceptionDictionary.BadRequest("A destination County is required.");

        var sourceIds = sourceCountyIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim())
            .Where(id => id != destinationCountyId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .ToList();
        if (sourceIds.Count == 0)
            throw ApiExceptionDictionary.BadRequest("At least one source County is required.");
        var actorUserId = !string.IsNullOrWhiteSpace(RequestUser.Id)
            ? RequestUser.Id
            : throw new InvalidOperationException("A current actor is required for County merge.");
        var mutationTimestamp = DateTime.UtcNow;

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var requestedIds = sourceIds.Append(destinationCountyId).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToList();
            var lockedCounties = await _countyLocks.LockForMutationAsync(requestedIds, cancellationToken);
            if (lockedCounties.Count != requestedIds.Count)
                throw ApiExceptionDictionary.BadRequest(
                    "One or more source or destination Counties do not exist in the current account.");

            var destination = lockedCounties.Single(county => county.Id == destinationCountyId);
            if (destination.StateId != stateId || lockedCounties.Any(county => county.StateId != stateId))
                throw ApiExceptionDictionary.BadRequest(
                    "All source and destination Counties must belong to the selected State.");

            var affectedIds = await DbContext.Caves.IgnoreQueryFilters().AsNoTracking()
                .Where(cave => cave.AccountId == _scope.AccountId && sourceIds.Contains(cave.CountyId))
                .OrderBy(cave => cave.Id).Select(cave => cave.Id).ToListAsync(cancellationToken);
            await LockCavesAsync(affectedIds, cancellationToken);

            var affected = affectedIds.Count == 0
                ? []
                : await DbContext.Caves.IgnoreQueryFilters().AsNoTracking()
                    .Where(cave => cave.AccountId == _scope.AccountId && affectedIds.Contains(cave.Id))
                    .Select(cave => new CountyMergeCave(cave.Id, cave.CountyId, cave.StateId, cave.CountyNumber,
                        cave.CurrentRevisionId))
                    .ToListAsync(cancellationToken);
            if (affected.Count != affectedIds.Count ||
                affected.Any(cave => !sourceIds.Contains(cave.CountyId) || cave.StateId != stateId))
                throw CountyAdministrationConcurrencyConflict();

            var destinationNumbers = (await DbContext.Caves.IgnoreQueryFilters().AsNoTracking()
                    .Where(cave => cave.AccountId == _scope.AccountId && cave.CountyId == destinationCountyId)
                    .Select(cave => cave.CountyNumber).ToListAsync(cancellationToken))
                .ToHashSet();
            var collisionNumber = affected.GroupBy(cave => cave.CountyNumber)
                .Where(group => group.Count() > 1 || destinationNumbers.Contains(group.Key))
                .Select(group => (int?)group.Key).OrderBy(number => number).FirstOrDefault();
            if (collisionNumber.HasValue) throw CountyNumberCollision(collisionNumber.Value);

            var expected = affected.ToDictionary(cave => cave.Id, cave => cave.CurrentRevisionId,
                StringComparer.Ordinal);
            var before = affectedIds.Count == 0
                ? new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal)
                : (await _snapshots.BuildManyAsync(affectedIds, cancellationToken))
                    .ToDictionary(snapshot => snapshot.CaveId, StringComparer.Ordinal);

            var updated = affectedIds.Count == 0 ? 0 : await DbContext.Caves.IgnoreQueryFilters()
                .Where(cave => cave.AccountId == _scope.AccountId && affectedIds.Contains(cave.Id) &&
                               sourceIds.Contains(cave.CountyId))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(cave => cave.CountyId, destinationCountyId)
                    .SetProperty(cave => cave.ModifiedOn, mutationTimestamp)
                    .SetProperty(cave => cave.ModifiedByUserId, actorUserId), cancellationToken);
            if (updated != affectedIds.Count) throw CountyAdministrationConcurrencyConflict();

            var after = affectedIds.Count == 0
                ? new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal)
                : (await _snapshots.BuildManyAsync(affectedIds, cancellationToken))
                    .ToDictionary(snapshot => snapshot.CaveId, StringComparer.Ordinal);
            var operations = affectedIds.ToDictionary(id => id, _ => CaveRevisionOperation.Update,
                StringComparer.Ordinal);
            await _revisions.PublishAsync(before, after, expected, operations, CaveRevisionSource.ManagerEdit,
                cancellationToken: cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (CaveRevisionConflictException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw CountyAdministrationConcurrencyConflict();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<CountyMutationResult> CreateOrUpdateAsync(string stateId, string name, string displayId,
        string? countyId, CancellationToken cancellationToken = default)
    {
        stateId = stateId.Trim();
        name = name.Trim();
        displayId = displayId.Trim();

        if (string.IsNullOrWhiteSpace(countyId))
            return await ExecuteCreateAsync(stateId, name, displayId, cancellationToken);

        return await ExecuteUpdateAsync(countyId.Trim(), stateId, name, displayId, cancellationToken);
    }

    private async Task<CountyMutationResult> ExecuteCreateAsync(string stateId, string name, string displayId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            await _countyLocks.LockAdministrationNamespaceAsync(cancellationToken);
            await ValidateTargetAsync(stateId, displayId, null, cancellationToken);
            var county = new County
            {
                AccountId = _scope.AccountId,
                StateId = stateId,
                Name = name,
                DisplayId = displayId
            };
            Add(county);
            await SaveWithDuplicateTranslationAsync(displayId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result(county);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<CountyMutationResult> ExecuteUpdateAsync(string countyId, string stateId, string name,
        string displayId, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            await _countyLocks.LockAdministrationNamespaceAsync(cancellationToken);
            var locked = await _countyLocks.LockForMutationAsync([countyId], cancellationToken);
            if (locked.Count != 1) throw ApiExceptionDictionary.NotFound("County Id");

            var county = await DbContext.Counties.IgnoreQueryFilters()
                .SingleOrDefaultAsync(row => row.AccountId == _scope.AccountId && row.Id == countyId,
                    cancellationToken)
                ?? throw ApiExceptionDictionary.NotFound("County Id");

            await ValidateTargetAsync(stateId, displayId, county.Id, cancellationToken);

            var stateChanged = county.StateId != stateId;
            var labelChanged = !string.Equals(county.Name, name, StringComparison.Ordinal) ||
                               !string.Equals(county.DisplayId, displayId, StringComparison.Ordinal);
            var affectedIds = stateChanged || labelChanged
                ? await DbContext.Caves.IgnoreQueryFilters().AsNoTracking()
                    .Where(cave => cave.AccountId == _scope.AccountId && cave.CountyId == county.Id)
                    .OrderBy(cave => cave.Id).Select(cave => cave.Id).ToListAsync(cancellationToken)
                : [];

            if (stateChanged && affectedIds.Count > 0)
                throw ApiExceptionDictionary.BadRequest(
                    "Cannot move county to another state because it is in use.");

            if (labelChanged) await LockCavesAsync(affectedIds, cancellationToken);
            var expected = labelChanged && affectedIds.Count > 0
                ? await DbContext.Caves.IgnoreQueryFilters().AsNoTracking()
                    .Where(cave => cave.AccountId == _scope.AccountId && affectedIds.Contains(cave.Id))
                    .ToDictionaryAsync(cave => cave.Id, cave => cave.CurrentRevisionId,
                        StringComparer.Ordinal, cancellationToken)
                : new Dictionary<string, string?>(StringComparer.Ordinal);
            var before = labelChanged && affectedIds.Count > 0
                ? (await _snapshots.BuildManyAsync(affectedIds, cancellationToken))
                    .ToDictionary(snapshot => snapshot.CaveId, StringComparer.Ordinal)
                : new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal);

            county.Name = name;
            county.DisplayId = displayId;
            county.StateId = stateId;
            await SaveWithDuplicateTranslationAsync(displayId, cancellationToken);

            if (labelChanged && affectedIds.Count > 0)
            {
                var after = (await _snapshots.BuildManyAsync(affectedIds, cancellationToken))
                    .ToDictionary(snapshot => snapshot.CaveId, StringComparer.Ordinal);
                var operations = affectedIds.ToDictionary(id => id, _ => CaveRevisionOperation.Update,
                    StringComparer.Ordinal);
                await _revisions.PublishAsync(before, after, expected, operations, CaveRevisionSource.ManagerEdit,
                    cancellationToken: cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result(county);
        }
        catch (CaveRevisionConflictException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw CountyAdministrationConcurrencyConflict();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<int> DeleteAsync(IEnumerable<string> countyIds,
        CancellationToken cancellationToken = default)
    {
        var ids = countyIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return 0;

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var locked = await _countyLocks.LockForMutationAsync(ids, cancellationToken);
            if (locked.Count != ids.Count) throw ApiExceptionDictionary.NotFound("County Id");

            var deleted = 0;
            try
            {
                foreach (var chunk in ids.Chunk(BatchSize))
                    deleted += await DbContext.Counties.IgnoreQueryFilters()
                        .Where(county => county.AccountId == _scope.AccountId && chunk.Contains(county.Id))
                        .ExecuteDeleteAsync(cancellationToken);
            }
            catch (Npgsql.PostgresException exception)
                when (exception.SqlState == Npgsql.PostgresErrorCodes.ForeignKeyViolation)
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

    private async Task ValidateTargetAsync(string stateId, string displayId, string? excludedCountyId,
        CancellationToken cancellationToken)
    {
        if (!await DbContext.States.AsNoTracking().AnyAsync(state => state.Id == stateId, cancellationToken))
            throw ApiExceptionDictionary.BadRequest("The selected State does not exist.");

        var duplicate = await DbContext.Counties.IgnoreQueryFilters().AsNoTracking().AnyAsync(county =>
            county.AccountId == _scope.AccountId && county.StateId == stateId && county.Id != excludedCountyId &&
            EF.Functions.ILike(county.DisplayId, displayId) && county.DisplayId.Length == displayId.Length,
            cancellationToken);
        if (duplicate) throw DuplicateCountyCode(displayId);
    }

    private async Task SaveWithDuplicateTranslationAsync(string displayId, CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException postgres &&
                                                   postgres.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            throw DuplicateCountyCode(displayId);
        }
    }

    private static ApiException DuplicateCountyCode(string displayId) =>
        ApiExceptionDictionary.BadRequest(
            $"The county code '{displayId}' is already in use for the selected state.");

    private async Task LockCavesAsync(IReadOnlyList<string> caveIds, CancellationToken cancellationToken)
    {
        if (caveIds.Count == 0) return;
        var connection = (NpgsqlConnection)DbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        var dbTransaction = DbContext.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
            ?? throw new InvalidOperationException("County administration transaction is not active.");

        foreach (var chunk in caveIds.Order(StringComparer.Ordinal).Chunk(BatchSize))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbTransaction;
            command.CommandText = """
                select "Id" from "Caves"
                where "AccountId" = @account_id and "Id" = any(@cave_ids)
                order by "Id" collate "C" for update
                """;
            command.Parameters.AddWithValue("account_id", _scope.AccountId);
            command.Parameters.AddWithValue("cave_ids", chunk);
            var count = 0;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) count++;
            if (count != chunk.Length) throw CountyAdministrationConcurrencyConflict();
        }
    }

    private static ApiException CountyNumberCollision(int countyNumber) =>
        ApiExceptionDictionary.BadRequest(
            $"Cannot merge Counties because County Number {countyNumber} would be duplicated in the destination County.");

    private static ApiException CountyAdministrationConcurrencyConflict() =>
        ApiExceptionDictionary.Conflict(
            "One or more affected Caves changed during County administration. Retry with current data.");

    private sealed record CountyMergeCave(
        string Id, string CountyId, string StateId, int CountyNumber, string? CurrentRevisionId);

    private static CountyMutationResult Result(County county) =>
        new(county.Id, county.Name, county.DisplayId);
}
