using Planarian.Model.Database.Revisions;
using Xunit;

namespace Planarian.Tests.Unit.Caves.Revisions;

public sealed class CaveRevisionNestedDiffTests
{
    private readonly CaveRevisionDiffService _diff = new();

    [Theory]
    [InlineData(nameof(CaveEntranceSnapshotV1.Name))]
    [InlineData(nameof(CaveEntranceSnapshotV1.IsPrimary))]
    [InlineData(nameof(CaveEntranceSnapshotV1.Description))]
    [InlineData(nameof(CaveEntranceSnapshotV1.Latitude))]
    [InlineData(nameof(CaveEntranceSnapshotV1.Longitude))]
    [InlineData(nameof(CaveEntranceSnapshotV1.Elevation))]
    [InlineData(nameof(CaveEntranceSnapshotV1.Srid))]
    [InlineData(nameof(CaveEntranceSnapshotV1.LocationQualityTagId))]
    [InlineData(nameof(CaveEntranceSnapshotV1.ReportedOn))]
    [InlineData(nameof(CaveEntranceSnapshotV1.PitDepthFeet))]
    public void EntranceScalarDetailsComeFromTheAuthoritativeComparison(string path)
    {
        var original = Entrance();
        var changed = path switch
        {
            nameof(CaveEntranceSnapshotV1.Name) => original with { Name = "North" },
            nameof(CaveEntranceSnapshotV1.IsPrimary) => original with { IsPrimary = true },
            nameof(CaveEntranceSnapshotV1.Description) => original with { Description = "Changed" },
            nameof(CaveEntranceSnapshotV1.Latitude) => original with { Latitude = 36 },
            nameof(CaveEntranceSnapshotV1.Longitude) => original with { Longitude = -87 },
            nameof(CaveEntranceSnapshotV1.Elevation) => original with { Elevation = 0 },
            nameof(CaveEntranceSnapshotV1.Srid) => original with { Srid = 4269 },
            nameof(CaveEntranceSnapshotV1.LocationQualityTagId) => original with
                { LocationQualityTagId = "estimated", LocationQualityNameAtRevision = "Estimated" },
            nameof(CaveEntranceSnapshotV1.ReportedOn) => original with
                { ReportedOn = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc) },
            nameof(CaveEntranceSnapshotV1.PitDepthFeet) => original with { PitDepthFeet = 0 },
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
        };

        var diff = _diff.Compare(Snapshot(original), Snapshot(changed));
        var detail = Assert.Single(diff.EntranceChanges);
        var scalar = Assert.Single(detail.Scalars);
        Assert.Equal(path, scalar.Key);
        Assert.Equal([original.Id], diff.ChangedEntrances);
    }

    [Fact]
    public void LatitudeAndLongitudeAreReportedIndependentlyByOneComparisonPath()
    {
        var original = Entrance();
        var diff = _diff.Compare(Snapshot(original), Snapshot(original with { Latitude = 36, Longitude = -87 }));

        var detail = Assert.Single(diff.EntranceChanges);
        Assert.Equal([nameof(CaveEntranceSnapshotV1.Latitude), nameof(CaveEntranceSnapshotV1.Longitude)],
            detail.Scalars.Keys);
        Assert.Equal([original.Id], diff.ChangedEntrances);
    }

    [Fact]
    public void NullableEntranceNumbersDistinguishNullFromZero()
    {
        var original = Entrance() with { Elevation = null, PitDepthFeet = null };
        var diff = _diff.Compare(Snapshot(original), Snapshot(original with { Elevation = 0, PitDepthFeet = 0 }));

        var detail = Assert.Single(diff.EntranceChanges);
        Assert.Null(detail.Scalars[nameof(CaveEntranceSnapshotV1.Elevation)].Previous);
        Assert.Equal(0d, detail.Scalars[nameof(CaveEntranceSnapshotV1.Elevation)].Current);
        Assert.Null(detail.Scalars[nameof(CaveEntranceSnapshotV1.PitDepthFeet)].Previous);
        Assert.Equal(0d, detail.Scalars[nameof(CaveEntranceSnapshotV1.PitDepthFeet)].Current);
    }

    [Fact]
    public void EntranceTagsAreDetailedByRoleAndBroadStatusFollowsDetail()
    {
        var original = Entrance() with
        {
            Tags = [new SnapshotTagReference(SnapshotTagRole.EntranceStatus, "closed", "Closed")]
        };
        var changed = original with
        {
            Tags = [new SnapshotTagReference(SnapshotTagRole.EntranceHydrology, "wet", "Wet")]
        };

        var diff = _diff.Compare(Snapshot(original), Snapshot(changed));

        var detail = Assert.Single(diff.EntranceChanges);
        Assert.Equal(SnapshotTagRole.EntranceHydrology, Assert.Single(detail.AddedTags).Role);
        Assert.Equal(SnapshotTagRole.EntranceStatus, Assert.Single(detail.RemovedTags).Role);
        Assert.Equal(detail.EntranceId, Assert.Single(diff.ChangedEntrances));
    }

    [Theory]
    [InlineData(nameof(CaveFileSnapshotV1.FileTypeTagId))]
    [InlineData(nameof(CaveFileSnapshotV1.Name))]
    [InlineData(nameof(CaveFileSnapshotV1.Extension))]
    public void FileScalarDetailsComeFromTheAuthoritativeComparison(string path)
    {
        var original = File();
        var changed = path switch
        {
            nameof(CaveFileSnapshotV1.FileTypeTagId) => original with
                { FileTypeTagId = "photo", FileTypeNameAtRevision = "Photo" },
            nameof(CaveFileSnapshotV1.Name) => original with { Name = "new" },
            nameof(CaveFileSnapshotV1.Extension) => original with { Extension = ".jpg" },
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
        };

        var diff = _diff.Compare(Snapshot(files: [original]), Snapshot(files: [changed]));
        var detail = Assert.Single(diff.FileChanges);
        Assert.Equal(path, Assert.Single(detail.Scalars).Key);
        Assert.Equal([original.Id], diff.ChangedFiles);
    }

    [Fact]
    public void StableNestedReferenceRenamesAreMetadataOnlyDetails()
    {
        var originalEntrance = Entrance() with
        {
            Tags = [new SnapshotTagReference(SnapshotTagRole.EntranceHydrology, "wet", "Wet")]
        };
        var changedEntrance = originalEntrance with
        {
            LocationQualityNameAtRevision = "Survey Grade",
            Tags = [new SnapshotTagReference(SnapshotTagRole.EntranceHydrology, "wet", "Water present")]
        };
        var originalFile = File();
        var changedFile = originalFile with { FileTypeNameAtRevision = "Cave Map" };

        var diff = _diff.Compare(Snapshot(originalEntrance, [originalFile]), Snapshot(changedEntrance, [changedFile]));

        Assert.Empty(Assert.Single(diff.EntranceChanges).Scalars);
        Assert.Empty(Assert.Single(diff.EntranceChanges).AddedTags);
        Assert.Empty(Assert.Single(diff.EntranceChanges).RemovedTags);
        Assert.Empty(Assert.Single(diff.FileChanges).Scalars);
        Assert.Equal(3, diff.ReferenceMetadataChanges.Count);
        Assert.Equal([originalEntrance.Id], diff.ChangedEntrances);
        Assert.Equal([originalFile.Id], diff.ChangedFiles);
    }

    [Fact]
    public void EntranceReportedByPeopleTagsAreSemanticFields()
    {
        var original = Entrance() with
        {
            Tags = [new SnapshotTagReference(SnapshotTagRole.EntranceReportedBy, "person-1", "Old Reporter")]
        };
        var changed = original with
        {
            Tags = [new SnapshotTagReference(SnapshotTagRole.EntranceReportedBy, "person-2", "New Reporter")]
        };

        var diff = _diff.Compare(Snapshot(original), Snapshot(changed));

        var detail = Assert.Single(diff.EntranceChanges);
        Assert.Empty(detail.Scalars);
        Assert.Equal("Old Reporter", Assert.Single(detail.RemovedTags).NameAtRevision);
        Assert.Equal("New Reporter", Assert.Single(detail.AddedTags).NameAtRevision);
    }

    [Fact]
    public void IsSemanticEqualRetainsNestedAndMetadataSemantics()
    {
        var entrance = Entrance();
        Assert.True(_diff.IsSemanticEqual(Snapshot(entrance), Snapshot(entrance)));
        Assert.False(_diff.IsSemanticEqual(Snapshot(entrance), Snapshot(entrance with { IsPrimary = true })));
        Assert.False(_diff.IsSemanticEqual(Snapshot(entrance), Snapshot(entrance with { LocationQualityNameAtRevision = "Renamed" })));
    }

    private static CavePublishedSnapshotV1 Snapshot(CaveEntranceSnapshotV1? entrance = null,
        IReadOnlyList<CaveFileSnapshotV1>? files = null) => new()
    {
        CaveId = "cave", AccountId = "account", Name = "Cave",
        State = new SnapshotReference("tn", "Tennessee"), County = new SnapshotReference("county", "County"),
        Entrances = entrance is null ? [] : [entrance], Files = files ?? []
    };

    private static CaveEntranceSnapshotV1 Entrance() => new()
    {
        Id = "entrance", Name = "Main", Description = "Original", Latitude = 35, Longitude = -86, Elevation = null,
        Srid = 4326, LocationQualityTagId = "exact", LocationQualityNameAtRevision = "Exact",
        PitDepthFeet = null
    };

    private static CaveFileSnapshotV1 File() => new()
    {
        Id = "file", FileTypeTagId = "map", FileTypeNameAtRevision = "Map",
        Name = "Map", Extension = ".pdf"
    };
}
