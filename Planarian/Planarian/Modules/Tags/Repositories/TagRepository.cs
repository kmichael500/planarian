using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
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

        var result = await DbContext.TagTypes
            .Where(e => e.Key == TagTypeKeyConstant.File && e.Name == fileTagTypeName && e.AccountId == accountId)
            .FirstOrDefaultAsync();
        
        if(result == null && fileTagTypeName == FileTypeTagName.Other)
        {
            var tagType = new TagType
            {
                Key = TagTypeKeyConstant.File,
                Name = FileTypeTagName.Other,
                AccountId = RequestUser.AccountId
            };
            DbContext.TagTypes.Add(tagType);
            await DbContext.SaveChangesAsync();
            return tagType;
        }
        return result;
    }
}