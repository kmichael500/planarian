using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Revisions;

public sealed record CaveRevisionQueryRow(
    CaveRevision Revision,
    string? ActorName,
    string? ChangeRequestSubmitterUserId,
    string? ChangeRequestSubmitterName,
    string? ChangeRequestReviewerName);

public sealed class CaveRevisionQueryRepository
{
    private readonly PlanarianDbContext _db;
    private readonly AccountExecutionScope _scope;

    public CaveRevisionQueryRepository(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
    }

    public async Task<(string? CurrentRevisionId, IReadOnlyList<CaveRevisionQueryRow>)?> ListAsync(
        string caveId, CancellationToken cancellationToken)
    {
        var currentRevisionId = await _db.Caves
            .Where(cave => cave.AccountId == _scope.AccountId && cave.Id == caveId)
            .Select(cave => cave.CurrentRevisionId)
            .SingleOrDefaultAsync(cancellationToken);

        if (currentRevisionId is null)
        {
            if (!await _db.Caves.AnyAsync(
                    cave => cave.AccountId == _scope.AccountId && cave.Id == caveId, cancellationToken))
                return null;
            throw new InvalidOperationException(
                $"Cave '{caveId}' has no current revision. Revision history rollout must complete before history is served.");
        }

        var rows = await (from revision in _db.CaveRevisions.AsNoTracking()
                          join user in _db.Users.AsNoTracking() on revision.CreatedByUserId equals user.Id into actors
                          from actor in actors.DefaultIfEmpty()
                          join requestEntity in _db.CaveChangeRequests.AsNoTracking()
                              on new { revision.AccountId, revision.CaveId, ChangeRequestId = revision.ChangeRequestId }
                              equals new
                              {
                                  requestEntity.AccountId,
                                  requestEntity.CaveId,
                                  ChangeRequestId = (string?)requestEntity.Id
                              } into requestRows
                          from request in requestRows.DefaultIfEmpty()
                          join submitterEntity in _db.Users.AsNoTracking() on request.CreatedByUserId equals submitterEntity.Id
                              into submitters
                          from submitter in submitters.DefaultIfEmpty()
                          join reviewerEntity in _db.Users.AsNoTracking() on request.ReviewerUserId equals reviewerEntity.Id
                              into reviewers
                          from reviewer in reviewers.DefaultIfEmpty()
                          where revision.AccountId == _scope.AccountId && revision.CaveId == caveId
                          select new CaveRevisionQueryRow(
                              revision,
                              actor == null ? null : actor.FirstName + " " + actor.LastName,
                              request == null ? null : request.CreatedByUserId,
                              submitter == null ? null : submitter.FirstName + " " + submitter.LastName,
                              reviewer == null ? null : reviewer.FirstName + " " + reviewer.LastName))
            .ToListAsync(cancellationToken);

        return (currentRevisionId, OrderByChain(currentRevisionId, rows));
    }

    public async Task<CaveRevisionQueryRow?> GetAsync(string caveId, string revisionId,
        CancellationToken cancellationToken) =>
        await (from revision in _db.CaveRevisions.AsNoTracking()
               join user in _db.Users.AsNoTracking() on revision.CreatedByUserId equals user.Id into actors
               from actor in actors.DefaultIfEmpty()
               join requestEntity in _db.CaveChangeRequests.AsNoTracking()
                   on new { revision.AccountId, revision.CaveId, ChangeRequestId = revision.ChangeRequestId }
                   equals new
                   {
                       requestEntity.AccountId,
                       requestEntity.CaveId,
                       ChangeRequestId = (string?)requestEntity.Id
                   } into requestRows
               from request in requestRows.DefaultIfEmpty()
               join submitterEntity in _db.Users.AsNoTracking() on request.CreatedByUserId equals submitterEntity.Id
                   into submitters
               from submitter in submitters.DefaultIfEmpty()
               join reviewerEntity in _db.Users.AsNoTracking() on request.ReviewerUserId equals reviewerEntity.Id
                   into reviewers
               from reviewer in reviewers.DefaultIfEmpty()
               where revision.AccountId == _scope.AccountId && revision.CaveId == caveId && revision.Id == revisionId
               select new CaveRevisionQueryRow(
                   revision,
                   actor == null ? null : actor.FirstName + " " + actor.LastName,
                   request == null ? null : request.CreatedByUserId,
                   submitter == null ? null : submitter.FirstName + " " + submitter.LastName,
                   reviewer == null ? null : reviewer.FirstName + " " + reviewer.LastName))
            .SingleOrDefaultAsync(cancellationToken);

    private static IReadOnlyList<CaveRevisionQueryRow> OrderByChain(string? currentRevisionId,
        IReadOnlyList<CaveRevisionQueryRow> rows)
    {
        if (currentRevisionId is null)
        {
            if (rows.Count != 0)
                throw new InvalidOperationException("The Cave revision history has records but no current revision.");
            return rows;
        }

        var byId = rows.ToDictionary(row => row.Revision.Id, StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var newestFirst = new List<CaveRevisionQueryRow>(rows.Count);
        var nextId = currentRevisionId;
        while (nextId is not null)
        {
            if (!visited.Add(nextId))
                throw new InvalidOperationException("The Cave revision history contains a cycle.");
            if (!byId.TryGetValue(nextId, out var row))
                throw new InvalidOperationException($"The Cave revision history is missing revision '{nextId}'.");
            newestFirst.Add(row);
            nextId = row.Revision.PreviousRevisionId;
        }

        if (visited.Count != rows.Count)
            throw new InvalidOperationException("The Cave revision history contains revisions outside the current chain.");

        newestFirst.Reverse();
        return newestFirst;
    }
}
