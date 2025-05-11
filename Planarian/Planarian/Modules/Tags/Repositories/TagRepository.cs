using System.Collections;
using LinqToDB;
using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Tags.Repositories;

public class TagRepository<TDbContext> : RepositoryBase<TDbContext> where TDbContext : PlanarianDbContextBase
{
    public TagRepository(TDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

    public async Task<TagType?> GetTag(string tagTypeId)
    {
        return await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(DbContext.TagTypes, e => e.Id == tagTypeId);
    }

    public async Task<TagType?> GetFileTypeTagByName(string fileTagTypeName, string? accountId = null)
    {
        var result = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(GetTagTypesQuery(TagTypeKeyConstant.File)
                .Where(e =>e.Name == fileTagTypeName));

        return result;
    }

    public async Task<TagType?> GetGeologyTagByName(string geologyName)
    {
        var result = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(GetTagTypesQuery(TagTypeKeyConstant.Geology)
                .Where(e => e.Name == geologyName));

        return result;
    }

    public async Task<IEnumerable<TagType>> GetGeologyTags()
    {
        return await EntityFrameworkQueryableExtensions.ToListAsync(GetTagTypesQuery(TagTypeKeyConstant.Geology));
    }

    public async Task<IEnumerable<County>> GetCounties()
    {
        return await EntityFrameworkQueryableExtensions.ToListAsync(DbContext.Counties.Where(e => e.AccountId == RequestUser.AccountId));
    }

    public async Task<IEnumerable<TagType>> LocationQualityTags()
    {
        return await EntityFrameworkQueryableExtensions.ToListAsync(GetTagTypesQuery(TagTypeKeyConstant.LocationQuality));
    }

    public async Task<IEnumerable<TagType>> GetEntranceHydrologyTags()
    {
        return await EntityFrameworkQueryableExtensions.ToListAsync(GetTagTypesQuery(TagTypeKeyConstant.EntranceHydrology));
    }

    public async Task<IEnumerable<TagType>> GetEntranceStatusTags()
    {
        return await EntityFrameworkQueryableExtensions.ToListAsync(GetTagTypesQuery(TagTypeKeyConstant.EntranceStatus));
    }

    public async Task<IEnumerable<TagType>> GetFieldIndicationTags()
    {
        return await EntityFrameworkQueryableExtensions.ToListAsync(GetTagTypesQuery(TagTypeKeyConstant.FieldIndication));
    }

    public async Task<IEnumerable<TagType>> GetTags(string key)
    {
        return await EntityFrameworkQueryableExtensions.ToListAsync(GetTagTypesQuery(key));
    }
    
    private IQueryable<TagType> GetTagTypesQuery(string key)
    {
        return DbContext.TagTypes
            .Where(e => e.Key == key &&
                        (e.AccountId == RequestUser.AccountId || e.IsDefault));
    }
    
    // only returns tag types that are arrays
    public async Task<IEnumerable<string>> GetAffectedCaveTagsIEnumerable(string tagTypeId, CancellationToken cancellationToken)
    {
        var caves = await EntityFrameworkQueryableExtensions.ToListAsync(DbContext.Caves
                .Where(c =>
                    c.AccountId == RequestUser.AccountId && (
                        c.GeologyTags.Any(gt => gt.TagTypeId == tagTypeId)
                        || c.MapStatusTags.Any(ms => ms.TagTypeId == tagTypeId)
                        || c.GeologicAgeTags.Any(gat => gat.TagTypeId == tagTypeId)
                        || c.PhysiographicProvinceTags.Any(pp => pp.TagTypeId == tagTypeId)
                        || c.BiologyTags.Any(bt => bt.TagTypeId == tagTypeId)
                        || c.ArcheologyTags.Any(at => at.TagTypeId == tagTypeId)
                        || c.CartographerNameTags.Any(ct => ct.TagTypeId == tagTypeId)
                        || c.CaveReportedByNameTags.Any(rt => rt.TagTypeId == tagTypeId)
                        || c.CaveOtherTags.Any(ot => ot.TagTypeId == tagTypeId)
                    )
                )
                .Select(e=>e.Id), cancellationToken);
        return caves;
    }
    
    public async Task<IEnumerable<(string EntranceId, string CaveId)>> GetEntrancesWithTagType(string tagTypeId,
        CancellationToken cancellationToken)
    {
        var entrances = await EntityFrameworkQueryableExtensions.ToListAsync(DbContext.Entrances
                .Where(c =>
                    c.Cave.AccountId == RequestUser.AccountId && (
                        c.LocationQualityTagId == tagTypeId
                        || c.EntranceStatusTags.Any(ms => ms.TagTypeId == tagTypeId)
                        || c.EntranceHydrologyTags.Any(gat => gat.TagTypeId == tagTypeId)
                        || c.FieldIndicationTags.Any(pp => pp.TagTypeId == tagTypeId)
                        || c.EntranceReportedByNameTags.Any(bt => bt.TagTypeId == tagTypeId
                        )
                    )
                )
                .Select(e => new { EntranceId = e.Id, CaveId = e.CaveId }), cancellationToken);
        return entrances.Select((e) => (e.EntranceId, e.CaveId));
    }
}

public class TagRepository : TagRepository<PlanarianDbContext>
{
    public TagRepository(PlanarianDbContext dbContext, RequestUser requestUser) : base(dbContext, requestUser)
    {
    }

 
}