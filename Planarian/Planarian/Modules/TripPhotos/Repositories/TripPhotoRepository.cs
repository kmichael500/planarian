using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.TripPhotos.Controllers;

public class TripPhotoRepository : RepositoryBase
{
    public TripPhotoRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<TripPhoto> GetTripPhoto(string tripPhotoId)
    {
        return await DbContext.Photos.FirstOrDefaultAsync(e =>
            e.Id == tripPhotoId && Enumerable.Any<ProjectMember>(e.TripObjective.Trip.Project.ProjectMembers, e => e.UserId == RequestUser.Id));
    }
}