using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Tags.Repositories;

public class TagRepository : RepositoryBase
{
    public TagRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<TagType?> GetTag(string tagTypeId)
    {
        return await DbContext.TagTypes.FirstOrDefaultAsync(e => e.Id == tagTypeId);
    }

    public async Task<TagType?> GetFileTypeTagByName(string fileTagTypeName, string? accountId = null)
    {
        var result = await GetTagTypesQuery(TagTypeKeyConstant.File)
            .Where(e =>e.Name == fileTagTypeName)
            .FirstOrDefaultAsync();

        return result;
    }

    public async Task<TagType?> GetGeologyTagByName(string geologyName)
    {
        var result = await GetTagTypesQuery(TagTypeKeyConstant.Geology)
            .Where(e => e.Name == geologyName)
            .FirstOrDefaultAsync();

        return result;
    }

    public async Task<IEnumerable<TagType>> GetGeologyTags()
    {
        return await GetTagTypesQuery(TagTypeKeyConstant.Geology)
            .ToListAsync();
    }

    public async Task<IEnumerable<County>> GetCounties()
    {
        return await DbContext.Counties.Where(e => e.AccountId == RequestUser.AccountId).ToListAsync();
    }

    public async Task<IEnumerable<TagType>> LocationQualityTags()
    {
        return await GetTagTypesQuery(TagTypeKeyConstant.LocationQuality)
            .ToListAsync();
    }

    public async Task<IEnumerable<TagType>> GetEntranceHydrologyTags()
    {
        return await GetTagTypesQuery(TagTypeKeyConstant.EntranceHydrology)
            .ToListAsync();
    }

    public async Task<IEnumerable<TagType>> GetEntranceStatusTags()
    {
        return await GetTagTypesQuery(TagTypeKeyConstant.EntranceStatus)
            .ToListAsync();
    }

    public async Task<IEnumerable<TagType>> GetFieldIndicationTags()
    {
        return await GetTagTypesQuery(TagTypeKeyConstant.FieldIndication)
            .ToListAsync();
    }

    public async Task<IEnumerable<TagType>> GetTags(string key)
    {
        return await GetTagTypesQuery(key).ToListAsync();
    }
    
    private IQueryable<TagType> GetTagTypesQuery(string key)
    {
        return DbContext.TagTypes
            .Where(e => e.Key == key &&
                        (e.AccountId == RequestUser.AccountId || string.IsNullOrWhiteSpace(e.AccountId) && e.IsDefault));
    }
}