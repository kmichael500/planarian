using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared;
using Planarian.Modules.Photos.Models;
using Planarian.Modules.Trips.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Trips.Repositories;

public class TripRepository : RepositoryBase
{
    public TripRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    #region Trip Member

    public async Task<TripMember?> DeleteTripMember(string tripId, string userId)
    {
        return await DbContext.TripMembers.FirstOrDefaultAsync(e =>
            e.UserId == userId && e.TripId == tripId);
    }

    #endregion

    public Tag GetTripTag(string tripTagId)
    {
        return DbContext.Tags.First(e => e.Id == tripTagId);
    }

    public async Task<TripIds?> GetIds(string tripId)
    {
        return await DbContext.Trips.Where(e => e.Id == tripId)
            .Select(e => new TripIds(e.ProjectId, e.Id)).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTripMembers(string tripId)
    {
        var tripMembers = await DbContext.Trips.Where(e => e.Id == tripId)
            .SelectMany(e => e.TripMembers)
            .Select(e => new SelectListItem<string>(e.User.FullName, e.UserId))
            .ToListAsync();

        return tripMembers;
    }

    public async Task<IEnumerable<PhotoVm>> GetTripPhotos(string tripId)
    {
        var photos = await DbContext.Trips.Where(e => e.Id == tripId)
            .SelectMany(e => e.Photos)
            .Select(e => new PhotoVm(e.Id, e.Name, e.Description, e.BlobKey!))
            .ToListAsync();

        return photos;
    }

    public async Task<IEnumerable<LeadVm>> GetLeads(string tripId)
    {
        return await DbContext.Lead.Where(e =>
                e.TripId == tripId &&
                e.Trip.Project.ProjectMembers.Any(e => e.UserId == RequestUser.Id))
            .Select(e => new LeadVm(e))
            .ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTripTags(string tripId)
    {
        return await DbContext.Trips.Where(e =>
                e.Id == tripId && e.Project.ProjectMembers.Any(ee => ee.UserId == RequestUser.Id))
            .SelectMany(e => e.TripTags)
            .Select(e => e.Tag)
            .Select(e => new SelectListItem<string>(e.Name, e.Id))
            .ToListAsync();
    }

    public async Task<TripTag?> GetTripTag(string tagId, string tripId)
    {
        return await DbContext.TripTags.Where(e =>
                e.TagId == tagId && e.TripId == tripId &&
                e.Trip.Project.ProjectMembers.Any(ee => ee.UserId == RequestUser.Id))
            .FirstOrDefaultAsync();
    }

    #region Trip

    public async Task<TripVm?> GetTripVm(string tripid)
    {
        var query = DbContext.Trips.Where(e => e.Id == tripid);

        return await ToTripVm(query).FirstOrDefaultAsync();
    }

    private static IQueryable<TripVm> ToTripVm(IQueryable<Trip> query)
    {
        return
            query
                .Select(e =>
                    new TripVm(e, e.TripTags.Select(ee => ee.TagId),
                        e.TripMembers.Select(ee => ee.UserId))
                );
    }

    public async Task<Trip?> GetTrip(string tripId)
    {
        return await DbContext.Trips.Where(e => e.Id == tripId).FirstOrDefaultAsync();
    }

    #endregion

    public async Task<IEnumerable<TripVm>> GetTripsByProjectId(string tripId)
    {
        return await DbContext.Trips.Where(e => e.ProjectId == tripId)
            .Select(e => new TripVm(e, e.TripTags.Select(e => e.TagId), e.TripMembers.Select(ee => ee.UserId)))
            .ToListAsync();
    }
}