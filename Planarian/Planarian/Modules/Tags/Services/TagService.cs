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
        var entity = new TagType(tagType.Name, key);

        switch (key)
        {
            case TagTypeKeyConstant.Default:
                break;

            # region Planarian

            case TagTypeKeyConstant.Trip:
                break;
            case TagTypeKeyConstant.Photo:
                break;

            #endregion

            #region Ridgewalker

            case TagTypeKeyConstant.LocationQuality:
            case TagTypeKeyConstant.Geology:
            case TagTypeKeyConstant.EntranceStatus:
            case TagTypeKeyConstant.FieldIndication:
            case TagTypeKeyConstant.EntranceHydrology:
            case TagTypeKeyConstant.EntranceHydrologyFrequency:
            case TagTypeKeyConstant.File:
                entity.AccountId = RequestUser.AccountId;
                break;
            #endregion

            default:
                throw ApiExceptionDictionary.BadRequest("Invalid tag key");
        }
        
        Repository.Add(entity);

        await Repository.SaveChangesAsync();
    }
}