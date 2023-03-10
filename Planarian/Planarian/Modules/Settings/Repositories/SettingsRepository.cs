using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Settings.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Settings.Repositories;

public class SettingsRepository : RepositoryBase
{
    public SettingsRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetTripTags()
    {
        return await DbContext.TagTypes.Select(e => new SelectListItem<string>(e.Name, e.Id)).ToListAsync();
    }

    public async Task<string> GetTagTypeName(string tagTypeId)
    {
        return await DbContext.TagTypes.Where(e => e.Id == tagTypeId).Select(e => e.Name).FirstAsync();
    }

    public async Task<NameProfilePhotoVm> GetUserNameProfilePhoto(string userId)
    {
        return await DbContext.Users.Where(e => e.Id == userId)
            .Select(e => new NameProfilePhotoVm(e.FullName, e.ProfilePhotoBlobKey)).FirstAsync();
    }

    public async Task<IEnumerable<SelectListItem<string>>> GetUsers()
    {
        return await DbContext.Users.Select(e => new SelectListItem<string>(e.FullName, e.Id)).ToListAsync();
    }
}