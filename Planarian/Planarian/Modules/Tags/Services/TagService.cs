using Planarian.Model.Database.Entities;
using Planarian.Model.Shared;
using Planarian.Modules.Tags.Models;
using Planarian.Modules.TripObjectives.Services;
using Planarian.Shared.Base;
using Planarian.Shared.Exceptions;

namespace Planarian.Modules.Tags.Services;

public class TagService : ServiceBase<TagRepository>
{
    public TagService(TagRepository repository, RequestUser requestUser) : base(repository, requestUser)
    {
    }

    public async Task CreateTag(CreateOrEditTagVm tag)
    {
        var key = tag.Key;

        if (!TagKey.IsValidTagKey(key)) throw ApiExceptionDictionary.BadRequest("Invalid tag key");

        var entity = new Tag(tag.Name, key);

        Repository.Add(entity);

        await Repository.SaveChangesAsync();
    }
}