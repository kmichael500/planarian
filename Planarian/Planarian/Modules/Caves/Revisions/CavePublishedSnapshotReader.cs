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
    private readonly AccountExecutionScope _scope;

    public CavePublishedSnapshotReader(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
    }

    public async Task<CavePublishedSnapshotV1> BuildAsync(string caveId, CancellationToken cancellationToken = default)
    {
        var cave = await Query().SingleOrDefaultAsync(c => c.Id == caveId, cancellationToken)
                   ?? throw new InvalidOperationException("Cave is not owned by the current account.");
        return Build(cave);
    }

    public async Task<List<CavePublishedSnapshotV1>> BuildManyAsync(IEnumerable<string> caveIds, CancellationToken cancellationToken = default)
    {
        var ids = caveIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        var caves = await Query().Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);
        if (caves.Count != ids.Count)
            throw new InvalidOperationException("One or more Caves are not owned by the current account.");
        return caves.Select(Build).ToList();
    }

    private IQueryable<Cave> Query() => _db.Caves.IgnoreQueryFilters()
        .Where(c => c.AccountId == _scope.AccountId)
        .AsNoTracking().AsSplitQuery()
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
        AddTags(cave.GeologyTags, tags, SnapshotTagRole.Geology, e => e.TagTypeId, e => e.TagType);
        AddTags(cave.GeologicAgeTags, tags, SnapshotTagRole.GeologicAge, e => e.TagTypeId, e => e.TagType);
        AddTags(cave.MapStatusTags, tags, SnapshotTagRole.MapStatus, e => e.TagTypeId, e => e.TagType);
        AddTags(cave.PhysiographicProvinceTags, tags, SnapshotTagRole.PhysiographicProvince, e => e.TagTypeId, e => e.TagType);
        AddTags(cave.BiologyTags, tags, SnapshotTagRole.Biology, e => e.TagTypeId, e => e.TagType);
        AddTags(cave.ArcheologyTags, tags, SnapshotTagRole.Archeology, e => e.TagTypeId, e => e.TagType);
        AddTags(cave.CartographerNameTags, tags, SnapshotTagRole.Cartographer, e => e.TagTypeId, e => e.TagType);
        AddTags(cave.CaveReportedByNameTags, tags, SnapshotTagRole.CaveReportedBy, e => e.TagTypeId, e => e.TagType);
        AddTags(cave.CaveOtherTags, tags, SnapshotTagRole.CaveOther, e => e.TagTypeId, e => e.TagType);
        return new CavePublishedSnapshotV1
        {
            CaveId = cave.Id, AccountId = cave.AccountId, Name = cave.Name,
            AlternateNames = cave.AlternateNamesList.Order(StringComparer.Ordinal).ToList(),
            State = new SnapshotReference(cave.StateId, cave.State.Name, null, cave.State.Abbreviation),
            County = new SnapshotReference(cave.CountyId, cave.County.Name, cave.County.DisplayId),
            CountyNumber = cave.CountyNumber, LengthFeet = cave.LengthFeet, DepthFeet = cave.DepthFeet,
            MaxPitDepthFeet = cave.MaxPitDepthFeet, NumberOfPits = cave.NumberOfPits, Narrative = cave.Narrative,
            ReportedByUserId = cave.ReportedByUserId, ReportedOn = cave.ReportedOn, IsArchived = cave.IsArchived,
            Tags = tags.OrderBy(t => t.Role).ThenBy(t => t.TagTypeId).ToList(),
            Entrances = cave.Entrances.OrderBy(e => e.Id).Select(BuildEntrance).ToList(),
            Files = cave.Files.OrderBy(f => f.Id).Select(f => new CaveFileSnapshotV1
            {
                Id = f.Id, FileTypeTagId = f.FileTypeTagId, FileTypeNameAtRevision = f.FileTypeTag.Name,
                FileName = f.FileName, DisplayName = f.DisplayName
            }).ToList()
        };
    }

    private static CaveEntranceSnapshotV1 BuildEntrance(Entrance e)
    {
        var tags = new List<SnapshotTagReference>();
        AddTags(e.EntranceStatusTags, tags, SnapshotTagRole.EntranceStatus, x => x.TagTypeId, x => x.TagType);
        AddTags(e.EntranceHydrologyTags, tags, SnapshotTagRole.EntranceHydrology, x => x.TagTypeId, x => x.TagType);
        AddTags(e.FieldIndicationTags, tags, SnapshotTagRole.FieldIndication, x => x.TagTypeId, x => x.TagType);
        AddTags(e.EntranceReportedByNameTags, tags, SnapshotTagRole.EntranceReportedBy, x => x.TagTypeId, x => x.TagType);
        AddTags(e.EntranceOtherTags, tags, SnapshotTagRole.EntranceOther, x => x.TagTypeId, x => x.TagType);
        return new CaveEntranceSnapshotV1
        {
            Id = e.Id, Name = e.Name, IsPrimary = e.IsPrimary, Description = e.Description, ReportedByUserId = e.ReportedByUserId,
            Latitude = e.Location?.Y, Longitude = e.Location?.X, Elevation = e.Location?.Z, Srid = e.Location?.SRID ?? 4326,
            LocationQualityTagId = e.LocationQualityTagId, LocationQualityNameAtRevision = e.LocationQualityTag.Name,
            ReportedOn = e.ReportedOn, PitDepthFeet = e.PitDepthFeet,
            Tags = tags.OrderBy(t => t.Role).ThenBy(t => t.TagTypeId).ToList()
        };
    }

    private static void AddTags<T>(IEnumerable<T> entities, ICollection<SnapshotTagReference> result,
        SnapshotTagRole role, Func<T, string> id, Func<T, TagType> tagType) where T : class
    {
        foreach (var entity in entities)
            result.Add(new SnapshotTagReference(role, id(entity), tagType(entity).Name));
    }
}
