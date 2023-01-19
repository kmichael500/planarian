using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.TripObjectives;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Trips.Repositories;

public class TripRepository : RepositoryBase
{
    public TripRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<TripVm?> GetTripVm(string tripId)
    {
        var query = DbContext.Trip.Where(e => e.Id == tripId);

        return await (await ToTripVm(query, tripId)).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TripVm>> GetTripsByProjectId(string projectId)
    {
        var trips = (await DbContext.Trip.Where(e =>
                    e.ProjectId == projectId && e.Project.ProjectMembers.Any(ee => ee.UserId == RequestUser.Id))
                .OrderBy(e => e.TripDate)
                .ToListAsync())
            .Select((e, i) => new TripVm(e, i + 1))
            .OrderBy(e => e.TripDate);

        return trips;
    }

    public async Task<int> GetTripNumber(string tripId)
    {
        var tripDate = await DbContext.Trip.Where(e => e.Id == tripId).Select(e => e.TripDate).FirstAsync();
        var tripNumber = await DbContext.Projects.Where(e => e.Trips.Any(e => e.Id == tripId))
            .SelectMany(e => e.Trips)
            .Where(e => e.TripDate <= tripDate)
            .Select(e => e.Id)
            .CountAsync();
        return tripNumber;
    }

    private async Task<IQueryable<TripVm>> ToTripVm(IQueryable<Trip> query, string tripId)
    {
        var tripNumber = await GetTripNumber(tripId);
        return query.Select(e => new TripVm(e, tripNumber));
    }


    public async Task<Trip?> GetTrip(string tripId)
    {
        return await DbContext.Trip.Where(e => e.Id == tripId).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TripObjectiveVm>> GetTripObjectives(string tripId)
    {
        var tripObjectives = await DbContext.Trip.Where(e => e.Id == tripId)
            .SelectMany(e => e.TripObjectives)
            .Select(e => new TripObjectiveVm(e, e.TripObjectiveTags.Select(ee => ee.TagId),
                e.TripObjectiveMembers.Select(ee => ee.UserId))).ToListAsync();

        return tripObjectives;
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTripMembers(string tripId)
    {
        var tripMembers = await DbContext.TripObjectiveMembers.Where(e => e.TripObjective.TripId == tripId)
            .Select(e => new SelectListItem<string>(e.User.FullName, e.UserId))
            .Distinct()
            .ToListAsync();

        return tripMembers;
    }
}