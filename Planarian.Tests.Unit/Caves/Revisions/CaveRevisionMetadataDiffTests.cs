using Planarian.Model.Database.Revisions;
using Xunit;

namespace Planarian.Tests;

public sealed class CaveRevisionMetadataDiffTests
{
    private readonly CaveRevisionDiffService _diff = new();

    [Fact]
    public void LocationQualityRenameIsMetadataChangeAndBroadEntranceChange()
    {
        var before = Snapshot() with
        {
            Entrances =
            [
                Entrance("entrance001", "quality001", "Approximate")
            ]
        };
        var after = Snapshot() with
        {
            Entrances =
            [
                Entrance("entrance001", "quality001", "Estimated")
            ]
        };

        var diff = _diff.Compare(before, after);

        Assert.Equal(["entrance001"], diff.ChangedEntrances);
        var change = Assert.Single(diff.ReferenceMetadataChanges);
        Assert.Equal("Entrances/entrance001/LocationQuality", change.Path);
        Assert.Equal("quality001", change.StableId);
        Assert.Equal(nameof(CaveEntranceSnapshotV1.LocationQualityNameAtRevision), change.Property);
        Assert.Equal("Approximate", change.PreviousValue);
        Assert.Equal("Estimated", change.CurrentValue);
        Assert.Empty(diff.AddedEntrances);
        Assert.Empty(diff.RemovedEntrances);
    }

    [Fact]
    public void FileTypeRenameIsMetadataChangeAndBroadFileChange()
    {
        var before = Snapshot() with
        {
            Files =
            [
                File("file000001", "filetype01", "Map")
            ]
        };
        var after = Snapshot() with
        {
            Files =
            [
                File("file000001", "filetype01", "Survey Map")
            ]
        };

        var diff = _diff.Compare(before, after);

        Assert.Equal(["file000001"], diff.ChangedFiles);
        var change = Assert.Single(diff.ReferenceMetadataChanges);
        Assert.Equal("Files/file000001/FileType", change.Path);
        Assert.Equal("filetype01", change.StableId);
        Assert.Equal(nameof(CaveFileSnapshotV1.FileTypeNameAtRevision), change.Property);
        Assert.Equal("Map", change.PreviousValue);
        Assert.Equal("Survey Map", change.CurrentValue);
        Assert.Empty(diff.AddedFiles);
        Assert.Empty(diff.RemovedFiles);
    }

    private static CavePublishedSnapshotV1 Snapshot() => new()
    {
        CaveId = "cave000001",
        AccountId = "acct000001",
        Name = "Test Cave",
        State = new SnapshotReference("state00001", "Tennessee", null, "TN"),
        County = new SnapshotReference("county0001", "Test County", "TST"),
        CountyNumber = 1
    };

    private static CaveEntranceSnapshotV1 Entrance(string id, string qualityId, string qualityName) => new()
    {
        Id = id,
        IsPrimary = true,
        Latitude = 35,
        Longitude = -86,
        Elevation = 700,
        Srid = 4326,
        LocationQualityTagId = qualityId,
        LocationQualityNameAtRevision = qualityName
    };

    private static CaveFileSnapshotV1 File(string id, string fileTypeId, string fileTypeName) => new()
    {
        Id = id,
        FileTypeTagId = fileTypeId,
        FileTypeNameAtRevision = fileTypeName,
        FileName = "map.pdf"
    };
}
