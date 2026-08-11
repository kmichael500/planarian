using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Revisions;

public sealed record CaveRevisionQueryRow(CaveRevision Revision, string? ActorName);

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

        if (currentRevisionId is null && !await _db.Caves.AnyAsync(
                cave => cave.AccountId == _scope.AccountId && cave.Id == caveId, cancellationToken))
            return null;

        var rows = await (from revision in _db.CaveRevisions.AsNoTracking()
                join user in _db.Users.AsNoTracking() on revision.CreatedByUserId equals user.Id into actors
                from actor in actors.DefaultIfEmpty()
                where revision.AccountId == _scope.AccountId && revision.CaveId == caveId
                orderby revision.CreatedOn, revision.Id
                select new CaveRevisionQueryRow(revision,
                    actor == null ? null : actor.FirstName + " " + actor.LastName))
            .ToListAsync(cancellationToken);

        return (currentRevisionId, rows);
    }

    public async Task<CaveRevisionQueryRow?> GetAsync(string caveId, string revisionId,
        CancellationToken cancellationToken) =>
        await (from revision in _db.CaveRevisions.AsNoTracking()
                join user in _db.Users.AsNoTracking() on revision.CreatedByUserId equals user.Id into actors
                from actor in actors.DefaultIfEmpty()
                where revision.AccountId == _scope.AccountId && revision.CaveId == caveId && revision.Id == revisionId
                select new CaveRevisionQueryRow(revision,
                    actor == null ? null : actor.FirstName + " " + actor.LastName))
            .SingleOrDefaultAsync(cancellationToken);
}
