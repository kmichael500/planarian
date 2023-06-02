using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared;
using Planarian.Modules.Leads.Controllers;
using Planarian.Modules.Photos.Models;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Modules.Trips.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Trips.Repositories;

public class TripRepository : RepositoryBase
{
    public TripRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    #region Trip Member

    public async Task<Member?> GetTripMember(string tripId, string userId)
    {
        return await DbContext.Members.FirstOrDefaultAsync(e =>
            e.UserId == userId && e.TripId == tripId);
    }

    #endregion

    public TagType GetTripTag(string tagTypeId)
    {
        return DbContext.TagTypes.First(e => e.Id == tagTypeId);
    }

    public async Task<TripIds?> GetIds(string tripId)
    {
        return await DbContext.Trips.Where(e => e.Id == tripId)
            .Select(e => new TripIds(e.ProjectId, e.Id)).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTripMembers(string tripId)
    {
        var tripMembers = await DbContext.Trips.Where(e => e.Id == tripId)
            .SelectMany(e => e.Members)
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

    public async Task<int> GetNumberOfTripPhotos(string tripId)
    {
        return await DbContext.Photos.CountAsync(e => e.Id == tripId);
    }

    public async Task<IEnumerable<LeadVm>> GetLeads(string tripId)
    {
        return await DbContext.Leads.Where(e =>
                e.TripId == tripId &&
                e.Trip.Project.Members.Any(e => e.UserId == RequestUser.Id))
            .Select(e => new LeadVm(e))
            .ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTripTags(string tripId)
    {
        return await DbContext.Trips.Where(e =>
                e.Id == tripId && e.Project.Members.Any(ee => ee.UserId == RequestUser.Id))
            .SelectMany(e => e.TripTags)
            .Select(e => e.TagType)
            .Select(e => new SelectListItem<string>(e.Name, e.Id))
            .ToListAsync();
    }

    public async Task<TripTag?> GetTripTag(string tagTypeId, string tripId)
    {
        return await DbContext.TripTags.Where(e =>
                e.TagTypeId == tagTypeId && e.TripId == tripId &&
                e.Trip.Project.Members.Any(ee => ee.UserId == RequestUser.Id))
            .FirstOrDefaultAsync();
    }

    public async Task<PagedResult<TripVm>> GetTripsByProjectIdAsQueryable(string projectId,
        FilterQuery query)
    {
        var result = await DbContext.Trips.Where(e => e.ProjectId == projectId)
            .Select(e => new TripVm
            {
                Id = e.Id,
                ProjectId = e.ProjectId,
                TripTagTypeIds = e.TripTags.Select(e => e.TagTypeId),
                TripMemberIds = e.Members.Select(ee => ee.UserId),
                Name = e.Name,
                Description = e.Description,
                TripReport = e.TripReport,
                NumberOfPhotos = e.Photos.Count,
                ModifiedOn = e.ModifiedOn,
                Leads = e.Leads.Select(e => new LeadVm(e)),
                Project = e.Project
            })
            // .IsInList(e=>e.TripTagTypeIds, new List<string>{"43T0lkwQ30", "Q1wx7qw5aU"})
            .QueryFilter(query.Conditions)
            .ApplyPagingAsync(query.PageNumber, query.PageSize, e=>e.ModifiedOn);
        
        return result;
    }

    #region Trip

    public async Task<TripVm?> GetTripVm(string tripId)
    {
        var query = DbContext.Trips.Where(e => e.Id == tripId);

        return await ToTripVm(query).FirstOrDefaultAsync();
    }

    private static IQueryable<TripVm> ToTripVm(IQueryable<Trip> query)
    {
        return
            query
                .Select(e =>
                    new TripVm(e, e.TripTags.Select(ee => ee.TagTypeId),
                        e.Members.Select(ee => ee.UserId), e.Photos.Count)
                );
    }

    public async Task<Trip?> GetTrip(string tripId)
    {
        return await DbContext.Trips.Where(e => e.Id == tripId).FirstOrDefaultAsync();
    }

    #endregion
}