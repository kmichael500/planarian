using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Caves.Controllers;

public class CaveRepository : RepositoryBase
{
    public CaveRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<PagedResult<CaveVm>> GetCaves(FilterQuery query)
    {
        var caves = await DbContext.Caves.Where(e=>e.AccountId == RequestUser.AccountId)
            .Select(e => new CaveVm
            {
                PrimaryEntranceId = e.PrimaryEntranceId,
                CountyId = e.CountyId,
                DisplayId = e.DisplayId,
                Name = e.Name,
                LengthFeet = e.LengthFeet,
                DepthFeet = e.DepthFeet,
                NumberOfPits = e.NumberOfPits,
                IsArchived = e.IsArchived,
                PrimaryEntrance = new EntranceVm
                {
                    Latitude = e.PrimaryEntrance.Latitude,
                    Longitude = e.PrimaryEntrance.Longitude,
                    ElevationFeet = e.PrimaryEntrance.ElevationFeet, 
                    EntranceStatusTagIds = e.PrimaryEntrance.EntranceStatusTags.Select(y => y.TagTypeId).ToList(), 
                    EntranceHydrologyFrequencyTagIds = e.PrimaryEntrance.EntranceHydrologyFrequencyTags.Select(y => y.TagTypeId).ToList(),
                    FieldIndicationTagIds = e.PrimaryEntrance.FieldIndicationTags.Select(y => y.TagTypeId).ToList(),
                    EntranceHydrologyTagIds = e.PrimaryEntrance.EntranceHydrologyTags.Select(y => y.TagTypeId).ToList(),
                },
                MapIds = e.Maps.Select(m => m.Id),
                EntranceIds = e.Entrances.Select(en => en.Id),
                GeologyTagIds = e.GeologyTags.Select(gt => gt.Id)
            })
            .QueryFilter(query.Conditions)
            .ApplyPagingAsync(query.PageNumber, query.PageSize, e=>e.ModifiedOn);

        return caves;
    }
}