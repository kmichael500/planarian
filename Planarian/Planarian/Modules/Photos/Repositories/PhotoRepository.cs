using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Photos.Repositories;

public class PhotoRepository : RepositoryBase
{
    public PhotoRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<Photo?> GetPhoto(string tripPhotoId)
    {
        return await DbContext.Photos.FirstOrDefaultAsync(e =>
            e.Id == tripPhotoId && e.Trip.Project.ProjectMembers.Any(ee => ee.UserId == RequestUser.Id));
    }
}