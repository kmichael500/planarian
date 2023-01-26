

using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.TripObjectives;
using Planarian.Model.Shared;
using Planarian.Modules.Leads.Models;
using Planarian.Modules.TripObjectives.Controllers;
using Planarian.Shared.Base;

namespace Planarian.Modules.TripObjectives.Repositories;

public class TripObjectiveRepository : RepositoryBase
{
    public TripObjectiveRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    #region Trip Objective Member

    public async Task<TripObjectiveMember?> GetTripObjectiveMember(string tripObjectiveId, string userId)
    {
        return await DbContext.TripObjectiveMembers.FirstOrDefaultAsync(e =>
            e.UserId == userId && e.TripObjectiveId == tripObjectiveId);
    }

    #endregion

    public Tag? GetTripObjectiveType(string tripObjectiveTypeId)
    {
        return DbContext.Tags.First(e => e.Id == tripObjectiveTypeId);
    }

    public async Task<TripObjectiveIds?> GetIds(string tripObjectiveId)
    {
        return await DbContext.TripObjectives.Where(e => e.Id == tripObjectiveId)
            .Select(e => new TripObjectiveIds(e.ProjectId, e.Id)).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTripObjectiveMembers(string tripObjectiveId)
    {
        var tripObjectiveMembers = await DbContext.TripObjectives.Where(e => e.Id == tripObjectiveId)
            .SelectMany(e => e.TripObjectiveMembers)
            .Select(e => new SelectListItem<string>(e.User.FullName, e.UserId))
            .ToListAsync();

        return tripObjectiveMembers;
    }

    public async Task<IEnumerable<TripPhotoVm>> GetTripObjectivePhotos(string tripObjectiveId)
    {
        var photos = await DbContext.TripObjectives.Where(e => e.Id == tripObjectiveId)
            .SelectMany(e => e.Photos)
            .Select(e => new TripPhotoVm(e.Id, e.Name, e.Description, e.BlobKey!))
            .ToListAsync();

        return photos;
    }

    public async Task<IEnumerable<LeadVm>> GetLeads(string tripObjectiveId)
    {
        return await DbContext.Lead.Where(e =>
                e.TripObjectiveId == tripObjectiveId &&
                e.TripObjective.Project.ProjectMembers.Any(e => e.UserId == RequestUser.Id))
            .Select(e => new LeadVm(e))
            .ToListAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTripObjectiveTags(string tripObjectiveId)
    {
        return await DbContext.TripObjectives.Where(e =>
                e.Id == tripObjectiveId && e.Project.ProjectMembers.Any(ee => ee.UserId == RequestUser.Id))
            .SelectMany(e => e.TripObjectiveTags)
            .Select(e => e.Tag)
            .Select(e => new SelectListItem<string>(e.Name, e.Id))
            .ToListAsync();
    }

    public async Task<TripObjectiveTag?> GetTripObjectiveTag(string tagId, string tripObjectiveId)
    {
        return await DbContext.TripObjectiveTag.Where(e =>
                e.TagId == tagId && e.TripObjectiveId == tripObjectiveId &&
                e.TripObjective.Project.ProjectMembers.Any(ee => ee.UserId == RequestUser.Id))
            .FirstOrDefaultAsync();
    }

    #region Trip Objective

    public async Task<TripObjectiveVm?> GetTripObjectiveVm(string tripObjectiveId)
    {
        var query = DbContext.TripObjectives.Where(e => e.Id == tripObjectiveId);

        return await ToTripObjectiveVm(query).FirstOrDefaultAsync();
    }

    private static IQueryable<TripObjectiveVm> ToTripObjectiveVm(IQueryable<TripObjective> query)
    {
        return
            query
                .Select(e =>
                    new TripObjectiveVm(e, e.TripObjectiveTags.Select(ee => ee.TagId),
                        e.TripObjectiveMembers.Select(ee => ee.UserId))
                );
    }

    public async Task<TripObjective?> GetTripObjective(string tripObjectivesId)
    {
        return await DbContext.TripObjectives.Where(e => e.Id == tripObjectivesId).FirstOrDefaultAsync();
    }

    #endregion

    public async Task<IEnumerable<TripObjectiveVm>> GetTripsByProjectId(string projectId)
    {
        return await DbContext.TripObjectives.Where(e => e.ProjectId == projectId)
            .Select(e => new TripObjectiveVm(e, e.TripObjectiveTags.Select(e => e.TagId), e.TripObjectiveMembers.Select(e=>e.UserId)))
            .ToListAsync();
    }
}

public class TripObjectiveIds
{
    public TripObjectiveIds(string projectId, string tripId)
    {
        ProjectId = projectId;
        TripId = tripId;
    }

    public TripObjectiveIds()
    {
    }

    public string ProjectId { get; set; } = null!;
    public string TripId { get; set; } = null!;
}