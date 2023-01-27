using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Modules.Tags.Models;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Exceptions;

namespace Planarian.Modules.Tags.Services;

public class TagService : ServiceBase<TagRepository>
{
    public TagService(TagRepository repository, RequestUser requestUser) : base(repository, requestUser)
    {
    }

    public async Task CreateTag(CreateOrEditTagTypeVm tagType)
    {
        var key = tagType.Key;

        if (!TagTypeKeyConstant.IsValidTagKey(key)) throw ApiExceptionDictionary.BadRequest("Invalid tag key");

        var entity = new TagType(tagType.Name, key);

        Repository.Add(entity);

        await Repository.SaveChangesAsync();
    }
}