using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Caves.Revisions;

namespace Planarian.Tests.Integration.Import.Golden;

internal sealed record GoldenCommittedState(
    List<string> TenantCaveKeys,
    List<string> TenantCountyCodes,
    List<GoldenCommittedCave> Caves,
    GoldenTagTypeDelta? TagTypes = null)
{
    public GoldenCommittedState WithExpectedTagTypes(IEnumerable<string> tagCreations) => TagTypes is not null
        ? this
        : this with
        {
            TagTypes = new GoldenTagTypeDelta(tagCreations.Select(value =>
            {
                var separator = value.IndexOf(':');
                return new GoldenTagType(value[..separator], value[(separator + 1)..], "account");
            }).OrderBy(tag => tag.Key).ThenBy(tag => tag.Name).ToList(), [])
        };

    public static async Task<GoldenCommittedState> CaptureAsync(Planarian.Model.Database.PlanarianDbContext db,
        string accountId, IReadOnlyDictionary<string, CaveMarker> before,
        IReadOnlyList<GoldenTagType> beforeTags)
    {
        var caveRows = await db.Caves.IgnoreQueryFilters().Where(cave => cave.AccountId == accountId)
            .Include(cave => cave.County).Include(cave => cave.State).AsNoTracking().ToListAsync();
        var reader = new CavePublishedSnapshotRepository(db, db.RequestUser);
        var caves = new List<GoldenCommittedCave>();
        foreach (var cave in caveRows.OrderBy(value => value.County.DisplayId).ThenBy(value => value.CountyNumber))
        {
            var snapshot = await reader.BuildAsync(cave.Id);
            var markerChanged = !before.TryGetValue(cave.Id, out var marker) ||
                                marker.Version != cave.Version || marker.RevisionId != cave.CurrentRevisionId;
            string? operation = null;
            if (markerChanged && cave.CurrentRevisionId is not null)
                operation = (await db.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
                    .SingleAsync(value => value.Id == cave.CurrentRevisionId)).Operation.ToString();
            var fileNames = await db.Files.IgnoreQueryFilters().Where(file => file.CaveId == cave.Id)
                .OrderBy(file => file.FileName).Select(file => file.FileName).ToListAsync();
            caves.Add(GoldenCommittedCave.From(snapshot, markerChanged, operation, fileNames));
        }

        var counties = await db.Counties.IgnoreQueryFilters().Where(county => county.AccountId == accountId)
            .OrderBy(county => county.DisplayId).Select(county => $"{county.DisplayId}:{county.Name}").ToListAsync();
        var afterTagRows = await db.TagTypes.IgnoreQueryFilters()
            .Where(tag => tag.AccountId == accountId || tag.IsDefault)
            .AsNoTracking().ToListAsync();
        var afterTags = afterTagRows.Select(tag => GoldenTagType.From(tag, accountId))
            .OrderBy(tag => tag.Key).ThenBy(tag => tag.Name).ThenBy(tag => tag.Ownership).ToList();
        return new GoldenCommittedState(caves.Select(cave => cave.Key).ToList(), counties, caves,
            new GoldenTagTypeDelta(afterTags.Except(beforeTags).ToList(), beforeTags.Except(afterTags).ToList()));
    }
}

internal sealed record GoldenTagType(string Key, string Name, string Ownership)
{
    public static GoldenTagType From(TagType tag, string accountId) =>
        new(tag.Key, tag.Name, tag.AccountId == accountId ? "account" : "default");
}

internal sealed record GoldenTagTypeDelta(List<GoldenTagType> Added, List<GoldenTagType> Removed);

internal sealed record GoldenCommittedCave(
    string Key, string Name, string State, string CountyName, List<string> AlternateNames,
    double? LengthFeet, double? DepthFeet, double? MaxPitDepthFeet, int? NumberOfPits,
    string? ReportedOn, bool IsArchived, string? Narrative, SortedDictionary<string, List<string>> Tags,
    List<GoldenCommittedEntrance> Entrances, List<string> FileNames,
    bool VersionOrRevisionChanged, string? LatestRevisionOperation)
{
    public static GoldenCommittedCave From(CavePublishedSnapshotV1 value, bool changed, string? operation,
        List<string> fileNames) => new(
        $"{value.County.DisplayIdAtRevision}-{value.CountyNumber}", value.Name,
        value.State.AbbreviationAtRevision ?? string.Empty, value.County.NameAtRevision,
        value.AlternateNames.Order().ToList(), value.LengthFeet, value.DepthFeet, value.MaxPitDepthFeet,
        value.NumberOfPits, GoldenSemantic.Date(value.ReportedOn), value.IsArchived, value.Narrative,
        GoldenSemantic.SnapshotTags(value.Tags), value.Entrances.Select(GoldenCommittedEntrance.From)
            .OrderBy(entrance => entrance.Name).ThenBy(entrance => entrance.Latitude).ToList(),
        fileNames, changed, operation);
}

internal sealed record GoldenCommittedEntrance(
    string? Name, bool IsPrimary, double? Latitude, double? Longitude, double? Elevation, int? Srid,
    string? LocationQuality, double? PitDepthFeet, string? ReportedOn, string? Description,
    SortedDictionary<string, List<string>> Tags)
{
    public static GoldenCommittedEntrance From(CaveEntranceSnapshotV1 value) => new(
        value.Name, value.IsPrimary, value.Latitude, value.Longitude, value.Elevation, value.Srid,
        value.LocationQualityNameAtRevision, value.PitDepthFeet, GoldenSemantic.Date(value.ReportedOn),
        value.Description, GoldenSemantic.SnapshotTags(value.Tags));
}
