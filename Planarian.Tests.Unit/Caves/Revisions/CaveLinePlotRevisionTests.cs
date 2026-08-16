using System.Text.Json;
using Planarian.Model.Database.Revisions;
using Xunit;

namespace Planarian.Tests.Unit.Caves.Revisions;

public sealed class CaveLinePlotRevisionTests
{
    private readonly CaveRevisionDiffService _diff = new();

    [Fact]
    public void CanonicalGeoJsonIgnoresObjectPropertyOrderingButPreservesArrayOrdering()
    {
        const string first = """{"type":"FeatureCollection","features":[{"type":"Feature","properties":{"b":2,"a":1},"geometry":null}]}""";
        const string reordered = """{"features":[{"geometry":null,"properties":{"a":1,"b":2},"type":"Feature"}],"type":"FeatureCollection"}""";
        const string reversedFeatures = """{"type":"FeatureCollection","features":[{"type":"Feature","properties":{"n":2},"geometry":null},{"type":"Feature","properties":{"n":1},"geometry":null}]}""";
        const string forwardFeatures = """{"type":"FeatureCollection","features":[{"type":"Feature","properties":{"n":1},"geometry":null},{"type":"Feature","properties":{"n":2},"geometry":null}]}""";

        Assert.Equal(CaveJsonContent.Normalize(first), CaveJsonContent.Normalize(reordered));
        Assert.Equal(CaveJsonContent.NormalizeAndHash(first).ContentHash,
            CaveJsonContent.NormalizeAndHash(reordered).ContentHash);
        Assert.NotEqual(CaveJsonContent.NormalizeAndHash(forwardFeatures).ContentHash,
            CaveJsonContent.NormalizeAndHash(reversedFeatures).ContentHash);
    }

    [Fact]
    public void LinePlotAuthoringRequiresAFeatureCollection()
    {
        Assert.Throws<JsonException>(() => CaveJsonContent.NormalizeFeatureCollection("{}"));
        Assert.Throws<JsonException>(() => CaveJsonContent.NormalizeFeatureCollection(
            """{"type":"Feature","features":[]}"""));
        Assert.Throws<JsonException>(() => CaveJsonContent.NormalizeFeatureCollection(
            """{"type":"FeatureCollection","features":{}}"""));

        Assert.Equal("{\"features\":[],\"type\":\"FeatureCollection\"}",
            CaveJsonContent.NormalizeFeatureCollection(
                """{"type":"FeatureCollection","features":[]}"""));
    }

    [Fact]
    public void StableLinePlotIdentityReportsRenameAndContentChangeWithoutRawPayloads()
    {
        var previous = Snapshot([
            new CaveLinePlotSnapshotV1 { Id = "line-a", Name = "Old name", ContentHash = "hash-a" }
        ]);
        var current = Snapshot([
            new CaveLinePlotSnapshotV1 { Id = "line-a", Name = "New name", ContentHash = "hash-b" }
        ]);

        var diff = _diff.Compare(previous, current);

        Assert.Equal(["line-a"], diff.ChangedLinePlots);
        Assert.Empty(diff.AddedLinePlots);
        Assert.Empty(diff.RemovedLinePlots);
        var detail = Assert.Single(diff.LinePlotChanges);
        Assert.Equal([nameof(CaveLinePlotSnapshotV1.Name), nameof(CaveLinePlotSnapshotV1.ContentHash)],
            detail.Scalars.Keys);
    }

    [Fact]
    public void LinePlotCollectionOrderingDoesNotCreateARevisionDifference()
    {
        var a = new CaveLinePlotSnapshotV1 { Id = "a", Name = "A", ContentHash = "hash-a" };
        var b = new CaveLinePlotSnapshotV1 { Id = "b", Name = "B", ContentHash = "hash-b" };

        Assert.True(_diff.IsSemanticEqual(Snapshot([a, b]), Snapshot([b, a])));
    }

    [Fact]
    public void LinePlotAddAndRemoveAreSemanticChanges()
    {
        var removed = new CaveLinePlotSnapshotV1 { Id = "old", Name = "Old", ContentHash = "old-hash" };
        var added = new CaveLinePlotSnapshotV1 { Id = "new", Name = "New", ContentHash = "new-hash" };

        var diff = _diff.Compare(Snapshot([removed]), Snapshot([added]));

        Assert.Equal(["new"], diff.AddedLinePlots);
        Assert.Equal(["old"], diff.RemovedLinePlots);
        Assert.False(_diff.IsSemanticEqual(Snapshot([removed]), Snapshot([added])));
    }

    private static CavePublishedSnapshotV1 Snapshot(IReadOnlyList<CaveLinePlotSnapshotV1> linePlots) => new()
    {
        CaveId = "cave", AccountId = "account", Name = "Cave",
        State = new SnapshotReference("tn", "Tennessee"),
        County = new SnapshotReference("county", "County"),
        LinePlots = linePlots
    };
}
