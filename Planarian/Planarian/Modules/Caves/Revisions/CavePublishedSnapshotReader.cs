using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Revisions;

public sealed class CavePublishedSnapshotReader
{
    private readonly PlanarianDbContext _db;
    public CavePublishedSnapshotReader(PlanarianDbContext db) => _db = db;

    public async Task<CavePublishedSnapshotV1> BuildAsync(string caveId, CancellationToken cancellationToken = default)
    {
        var cave = await Query().SingleAsync(c => c.Id == caveId, cancellationToken);
        return Build(cave);
    }

    public async Task<List<CavePublishedSnapshotV1>> BuildManyAsync(IEnumerable<string> caveIds, CancellationToken cancellationToken = default)
    {
        var ids = caveIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        var caves = await Query().Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);
        return caves.Select(Build).ToList();
    }

    private IQueryable<Cave> Query() => _db.Caves.IgnoreQueryFilters().AsNoTracking().AsSplitQuery()
        .Include(c => c.State).Include(c => c.County)
        .Include(c => c.Files).ThenInclude(f => f.FileTypeTag)
        .Include(c => c.Entrances).ThenInclude(e => e.LocationQualityTag)
        .Include(c => c.Entrances).ThenInclude(e => e.EntranceStatusTags).ThenInclude(t => t.TagType)
        .Include(c => c.Entrances).ThenInclude(e => e.EntranceHydrologyTags).ThenInclude(t => t.TagType)
        .Include(c => c.Entrances).ThenInclude(e => e.FieldIndicationTags).ThenInclude(t => t.TagType)
        .Include(c => c.Entrances).ThenInclude(e => e.EntranceReportedByNameTags).ThenInclude(t => t.TagType)
        .Include(c => c.Entrances).ThenInclude(e => e.EntranceOtherTags).ThenInclude(t => t.TagType)
        .Include(c => c.GeologyTags).ThenInclude(t => t.TagType)
        .Include(c => c.GeologicAgeTags).ThenInclude(t => t.TagType)
        .Include(c => c.MapStatusTags).ThenInclude(t => t.TagType)
        .Include(c => c.PhysiographicProvinceTags).ThenInclude(t => t.TagType)
        .Include(c => c.BiologyTags).ThenInclude(t => t.TagType)
        .Include(c => c.ArcheologyTags).ThenInclude(t => t.TagType)
        .Include(c => c.CartographerNameTags).ThenInclude(t => t.TagType)
        .Include(c => c.CaveReportedByNameTags).ThenInclude(t => t.TagType)
        .Include(c => c.CaveOtherTags).ThenInclude(t => t.TagType);

    private static CavePublishedSnapshotV1 Build(Cave cave)
    {
        var tags = new List<SnapshotTagReference>();
        AddTags(cave.GeologyTags, tags, TagTypeKeyConstant.Geology);
        AddTags(cave.GeologicAgeTags, tags, TagTypeKeyConstant.GeologicAge);
        AddTags(cave.MapStatusTags, tags, TagTypeKeyConstant.MapStatus);
        AddTags(cave.PhysiographicProvinceTags, tags, TagTypeKeyConstant.PhysiographicProvince);
        AddTags(cave.BiologyTags, tags, TagTypeKeyConstant.Biology);
        AddTags(cave.ArcheologyTags, tags, TagTypeKeyConstant.Archeology);
        AddTags(cave.CartographerNameTags, tags, TagTypeKeyConstant.People);
        AddTags(cave.CaveReportedByNameTags, tags, TagTypeKeyConstant.People);
        AddTags(cave.CaveOtherTags, tags, TagTypeKeyConstant.CaveOther);
        return new CavePublishedSnapshotV1
        {
            CaveId = cave.Id, AccountId = cave.AccountId, Name = cave.Name,
            AlternateNames = cave.AlternateNamesList.Order(StringComparer.Ordinal).ToList(),
            State = new SnapshotReference(cave.StateId, cave.State.Name), County = new SnapshotReference(cave.CountyId, cave.County.Name),
            CountyNumber = cave.CountyNumber, LengthFeet = cave.LengthFeet, DepthFeet = cave.DepthFeet,
            MaxPitDepthFeet = cave.MaxPitDepthFeet, NumberOfPits = cave.NumberOfPits, Narrative = cave.Narrative,
            ReportedOn = cave.ReportedOn, IsArchived = cave.IsArchived,
            Tags = tags.OrderBy(t => t.TagTypeId).ThenBy(t => t.Key).ToList(),
            Entrances = cave.Entrances.OrderBy(e => e.Id).Select(BuildEntrance).ToList(),
            Files = cave.Files.OrderBy(f => f.Id).Select(f => new CaveFileSnapshotV1
            {
                Id = f.Id, FileTypeTagId = f.FileTypeTagId, FileTypeNameAtRevision = f.FileTypeTag.Name,
                FileName = f.FileName, DisplayName = f.DisplayName, BlobContainer = f.BlobContainer
            }).ToList()
        };
    }

    private static CaveEntranceSnapshotV1 BuildEntrance(Entrance e)
    {
        var tags = new List<SnapshotTagReference>();
        AddTags(e.EntranceStatusTags, tags, TagTypeKeyConstant.EntranceStatus);
        AddTags(e.EntranceHydrologyTags, tags, TagTypeKeyConstant.EntranceHydrology);
        AddTags(e.FieldIndicationTags, tags, TagTypeKeyConstant.FieldIndication);
        AddTags(e.EntranceReportedByNameTags, tags, TagTypeKeyConstant.People);
        AddTags(e.EntranceOtherTags, tags, "EntranceOther");
        return new CaveEntranceSnapshotV1
        {
            Id = e.Id, Name = e.Name, IsPrimary = e.IsPrimary, Description = e.Description,
            Latitude = e.Location?.Y, Longitude = e.Location?.X, Elevation = e.Location?.Z,
            LocationQualityTagId = e.LocationQualityTagId, LocationQualityNameAtRevision = e.LocationQualityTag.Name,
            ReportedOn = e.ReportedOn, PitDepthFeet = e.PitDepthFeet,
            Tags = tags.OrderBy(t => t.TagTypeId).ThenBy(t => t.Key).ToList()
        };
    }

    private static void AddTags<T>(IEnumerable<T> entities, ICollection<SnapshotTagReference> result, string key) where T : class
    {
        foreach (var entity in entities)
        {
            var tagType = entity.GetType().GetProperty("TagType")?.GetValue(entity) as TagType;
            var tagTypeId = entity.GetType().GetProperty("TagTypeId")?.GetValue(entity) as string;
            if (tagType is not null && tagTypeId is not null) result.Add(new SnapshotTagReference(tagTypeId, tagType.Name, key));
        }
    }
}
