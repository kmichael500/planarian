using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Model.Database;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Revisions;

public sealed class CavePublishedSnapshotRepository
{
    private const int ChunkSize = 2500;
    private readonly PlanarianDbContext _db;
    private readonly AccountExecutionScope _scope;

    public CavePublishedSnapshotRepository(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
    }

    public async Task<CavePublishedSnapshotV1> BuildAsync(string caveId, CancellationToken cancellationToken = default)
    {
        var snapshots = await BuildManyAsync([caveId], cancellationToken);
        return snapshots.Single();
    }

    public async Task<List<CavePublishedSnapshotV1>> BuildManyAsync(IEnumerable<string> caveIds,
        CancellationToken cancellationToken = default)
    {
        var ids = caveIds.Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return [];

        var snapshotsById = new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal);
        foreach (var chunk in ids.Chunk(ChunkSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cores = await LoadCoreAsync(chunk, cancellationToken);
            if (cores.Count != chunk.Length)
                throw new InvalidOperationException("One or more Caves are not owned by the current account.");

            // Five bounded projection groups per chunk: core, cave tags,
            // entrances, entrance tags, and files. No tracked aggregate graph is
            // materialized and memory usage scales with ChunkSize, not import size.
            var caveTags = await LoadCaveTagsAsync(chunk, cancellationToken);
            var entrances = await LoadEntrancesAsync(chunk, cancellationToken);
            var entranceIds = entrances.Select(e => e.Id).ToArray();
            var entranceTags = await LoadEntranceTagsAsync(chunk, entranceIds, cancellationToken);
            var files = await LoadFilesAsync(chunk, cancellationToken);

            var caveTagsByCave = caveTags.GroupBy(t => t.CaveId)
                .ToDictionary(g => g.Key,
                    g => (IReadOnlyList<SnapshotTagReference>)g
                        .Select(t => new SnapshotTagReference(t.Role, t.TagTypeId, t.Name))
                        .OrderBy(t => t.Role).ThenBy(t => t.TagTypeId, StringComparer.Ordinal).ToList(),
                    StringComparer.Ordinal);
            var entranceTagsByEntrance = entranceTags.GroupBy(t => t.EntranceId)
                .ToDictionary(g => g.Key,
                    g => (IReadOnlyList<SnapshotTagReference>)g
                        .Select(t => new SnapshotTagReference(t.Role, t.TagTypeId, t.Name))
                        .OrderBy(t => t.Role).ThenBy(t => t.TagTypeId, StringComparer.Ordinal).ToList(),
                    StringComparer.Ordinal);
            var entrancesByCave = entrances.GroupBy(e => e.CaveId)
                .ToDictionary(g => g.Key,
                    g => (IReadOnlyList<CaveEntranceSnapshotV1>)g.OrderBy(e => e.Id, StringComparer.Ordinal)
                        .Select(e => new CaveEntranceSnapshotV1
                        {
                            Id = e.Id,
                            Name = e.Name,
                            IsPrimary = e.IsPrimary,
                            Description = e.Description,
                            ReportedByUserId = e.ReportedByUserId,
                            Latitude = e.Location?.Y,
                            Longitude = e.Location?.X,
                            Elevation = e.Location?.Z,
                            Srid = e.Location?.SRID ?? 4326,
                            LocationQualityTagId = e.LocationQualityTagId,
                            LocationQualityNameAtRevision = e.LocationQualityName,
                            ReportedOn = e.ReportedOn,
                            PitDepthFeet = e.PitDepthFeet,
                            Tags = entranceTagsByEntrance.GetValueOrDefault(e.Id) ?? []
                        }).ToList(),
                    StringComparer.Ordinal);
            var filesByCave = files.GroupBy(f => f.CaveId)
                .ToDictionary(g => g.Key,
                    g => (IReadOnlyList<CaveFileSnapshotV1>)g.OrderBy(f => f.Id, StringComparer.Ordinal)
                        .Select(f => new CaveFileSnapshotV1
                        {
                            Id = f.Id,
                            FileTypeTagId = f.FileTypeTagId,
                            FileTypeNameAtRevision = f.FileTypeName,
                            FileName = f.FileName,
                            DisplayName = f.DisplayName
                        }).ToList(),
                    StringComparer.Ordinal);

            foreach (var core in cores)
            {
                var alternateNames = JsonSerializer.Deserialize<List<string>>(core.AlternateNames) ?? [];
                snapshotsById[core.Id] = new CavePublishedSnapshotV1
                {
                    CaveId = core.Id,
                    AccountId = core.AccountId,
                    Name = core.Name,
                    AlternateNames = alternateNames.Order(StringComparer.Ordinal).ToList(),
                    State = new SnapshotReference(core.StateId, core.StateName, null, core.StateAbbreviation),
                    County = new SnapshotReference(core.CountyId, core.CountyName, core.CountyDisplayId),
                    CountyNumber = core.CountyNumber,
                    ReportedByUserId = core.ReportedByUserId,
                    LengthFeet = core.LengthFeet,
                    DepthFeet = core.DepthFeet,
                    MaxPitDepthFeet = core.MaxPitDepthFeet,
                    NumberOfPits = core.NumberOfPits,
                    Narrative = core.Narrative,
                    ReportedOn = core.ReportedOn,
                    IsArchived = core.IsArchived,
                    Tags = caveTagsByCave.GetValueOrDefault(core.Id) ?? [],
                    Entrances = entrancesByCave.GetValueOrDefault(core.Id) ?? [],
                    Files = filesByCave.GetValueOrDefault(core.Id) ?? []
                };
            }
        }

        if (snapshotsById.Count != ids.Count)
            throw new InvalidOperationException("One or more Caves are not owned by the current account.");
        return ids.Select(id => snapshotsById[id]).ToList();
    }

    private Task<List<CaveCoreRow>> LoadCoreAsync(string[] caveIds, CancellationToken cancellationToken) =>
        _db.Caves.IgnoreQueryFilters()
            .Where(c => c.AccountId == _scope.AccountId && caveIds.Contains(c.Id))
            .AsNoTracking()
            .Select(c => new CaveCoreRow(
                c.Id, c.AccountId, c.Name, c.AlternateNames, c.StateId, c.State.Name, c.State.Abbreviation,
                c.CountyId, c.County.Name, c.County.DisplayId, c.CountyNumber, c.ReportedByUserId,
                c.LengthFeet, c.DepthFeet, c.MaxPitDepthFeet, c.NumberOfPits, c.Narrative, c.ReportedOn,
                c.IsArchived))
            .ToListAsync(cancellationToken);

    private async Task<List<CaveTagRow>> LoadCaveTagsAsync(string[] caveIds, CancellationToken cancellationToken)
    {
        var geology = _db.GeologyTags.IgnoreQueryFilters()
            .Where(t => caveIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId)
            .Select(t => new { t.CaveId, Role = (int)SnapshotTagRole.Geology, t.TagTypeId, t.TagType.Name });
        var geologicAge = _db.GeologicAgeTags.IgnoreQueryFilters()
            .Where(t => caveIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId)
            .Select(t => new { t.CaveId, Role = (int)SnapshotTagRole.GeologicAge, t.TagTypeId, t.TagType.Name });
        var mapStatus = _db.MapStatusTags.IgnoreQueryFilters()
            .Where(t => caveIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId)
            .Select(t => new { t.CaveId, Role = (int)SnapshotTagRole.MapStatus, t.TagTypeId, t.TagType.Name });
        var physiographic = _db.PhysiographicProvinceTags.IgnoreQueryFilters()
            .Where(t => caveIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId)
            .Select(t => new { t.CaveId, Role = (int)SnapshotTagRole.PhysiographicProvince, t.TagTypeId, t.TagType.Name });
        var archeology = _db.ArcheologyTags.IgnoreQueryFilters()
            .Where(t => caveIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId)
            .Select(t => new { t.CaveId, Role = (int)SnapshotTagRole.Archeology, t.TagTypeId, t.TagType.Name });
        var biology = _db.BiologyTags.IgnoreQueryFilters()
            .Where(t => caveIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId)
            .Select(t => new { t.CaveId, Role = (int)SnapshotTagRole.Biology, t.TagTypeId, t.TagType.Name });
        var other = _db.CaveOtherTags.IgnoreQueryFilters()
            .Where(t => caveIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId)
            .Select(t => new { t.CaveId, Role = (int)SnapshotTagRole.CaveOther, t.TagTypeId, t.TagType.Name });
        var cartographer = _db.CartographerNameTags.IgnoreQueryFilters()
            .Where(t => caveIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId)
            .Select(t => new { t.CaveId, Role = (int)SnapshotTagRole.Cartographer, t.TagTypeId, t.TagType.Name });
        var reportedBy = _db.CaveReportedByNameTags.IgnoreQueryFilters()
            .Where(t => caveIds.Contains(t.CaveId) && t.Cave != null && t.Cave.AccountId == _scope.AccountId)
            .Select(t => new { t.CaveId, Role = (int)SnapshotTagRole.CaveReportedBy, t.TagTypeId, t.TagType.Name });

        var rows = await geology.Concat(geologicAge).Concat(mapStatus).Concat(physiographic).Concat(archeology)
            .Concat(biology).Concat(other).Concat(cartographer).Concat(reportedBy)
            .AsNoTracking().ToListAsync(cancellationToken);
        return rows.Select(row => new CaveTagRow(row.CaveId, (SnapshotTagRole)row.Role, row.TagTypeId, row.Name))
            .ToList();
    }

    private Task<List<EntranceRow>> LoadEntrancesAsync(string[] caveIds, CancellationToken cancellationToken) =>
        _db.Entrances.IgnoreQueryFilters()
            .Where(e => caveIds.Contains(e.CaveId) && e.Cave != null && e.Cave.AccountId == _scope.AccountId)
            .AsNoTracking()
            .Select(e => new EntranceRow(e.Id, e.CaveId, e.Name, e.IsPrimary, e.Description, e.ReportedByUserId,
                e.Location, e.LocationQualityTagId, e.LocationQualityTag.Name, e.ReportedOn, e.PitDepthFeet))
            .ToListAsync(cancellationToken);

    private async Task<List<EntranceTagRow>> LoadEntranceTagsAsync(string[] caveIds, string[] entranceIds,
        CancellationToken cancellationToken)
    {
        if (entranceIds.Length == 0) return [];
        var status = _db.EntranceStatusTags.IgnoreQueryFilters()
            .Where(t => entranceIds.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null &&
                        t.Entrance.Cave.AccountId == _scope.AccountId && caveIds.Contains(t.Entrance.CaveId))
            .Select(t => new { t.EntranceId, Role = (int)SnapshotTagRole.EntranceStatus, t.TagTypeId, t.TagType.Name });
        var hydrology = _db.EntranceHydrologyTags.IgnoreQueryFilters()
            .Where(t => entranceIds.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null &&
                        t.Entrance.Cave.AccountId == _scope.AccountId && caveIds.Contains(t.Entrance.CaveId))
            .Select(t => new { t.EntranceId, Role = (int)SnapshotTagRole.EntranceHydrology, t.TagTypeId, t.TagType.Name });
        var field = _db.FieldIndicationTags.IgnoreQueryFilters()
            .Where(t => entranceIds.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null &&
                        t.Entrance.Cave.AccountId == _scope.AccountId && caveIds.Contains(t.Entrance.CaveId))
            .Select(t => new { t.EntranceId, Role = (int)SnapshotTagRole.FieldIndication, t.TagTypeId, t.TagType.Name });
        var reportedBy = _db.EntranceReportedByNameTags.IgnoreQueryFilters()
            .Where(t => entranceIds.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null &&
                        t.Entrance.Cave.AccountId == _scope.AccountId && caveIds.Contains(t.Entrance.CaveId))
            .Select(t => new { t.EntranceId, Role = (int)SnapshotTagRole.EntranceReportedBy, t.TagTypeId, t.TagType.Name });
        var other = _db.EntranceOtherTag.IgnoreQueryFilters()
            .Where(t => entranceIds.Contains(t.EntranceId) && t.Entrance != null && t.Entrance.Cave != null &&
                        t.Entrance.Cave.AccountId == _scope.AccountId && caveIds.Contains(t.Entrance.CaveId))
            .Select(t => new { t.EntranceId, Role = (int)SnapshotTagRole.EntranceOther, t.TagTypeId, t.TagType.Name });

        var rows = await status.Concat(hydrology).Concat(field).Concat(reportedBy).Concat(other)
            .AsNoTracking().ToListAsync(cancellationToken);
        return rows.Select(row => new EntranceTagRow(row.EntranceId, (SnapshotTagRole)row.Role, row.TagTypeId, row.Name))
            .ToList();
    }

    private Task<List<FileRow>> LoadFilesAsync(string[] caveIds, CancellationToken cancellationToken) =>
        _db.Files.IgnoreQueryFilters()
            .Where(f => f.CaveId != null && caveIds.Contains(f.CaveId) && f.Cave != null &&
                        f.Cave.AccountId == _scope.AccountId)
            .AsNoTracking()
            .Select(f => new FileRow(f.CaveId!, f.Id, f.FileTypeTagId, f.FileTypeTag.Name, f.FileName, f.DisplayName))
            .ToListAsync(cancellationToken);

    private sealed record CaveCoreRow(
        string Id, string AccountId, string Name, string AlternateNames,
        string StateId, string StateName, string StateAbbreviation,
        string CountyId, string CountyName, string CountyDisplayId, int CountyNumber,
        string? ReportedByUserId, double? LengthFeet, double? DepthFeet, double? MaxPitDepthFeet,
        int? NumberOfPits, string? Narrative, DateTime? ReportedOn, bool IsArchived);

    private sealed record CaveTagRow(string CaveId, SnapshotTagRole Role, string TagTypeId, string Name);

    private sealed record EntranceRow(
        string Id, string CaveId, string? Name, bool IsPrimary, string? Description, string? ReportedByUserId,
        Point? Location, string LocationQualityTagId, string LocationQualityName, DateTime? ReportedOn,
        double? PitDepthFeet);

    private sealed record EntranceTagRow(string EntranceId, SnapshotTagRole Role, string TagTypeId, string Name);
    private sealed record FileRow(string CaveId, string Id, string FileTypeTagId, string FileTypeName,
        string FileName, string? DisplayName);
}
