using Planarian.Model.Database.Revisions;
using Xunit;

namespace Planarian.Tests.Unit.Caves.Revisions;

public sealed class CaveProposalChangeScopeSummaryTests
{
    [Fact]
    public void SummarizeUsesAuthoritativeDiffSemanticsAcrossAggregateAssets()
    {
        var before = Snapshot();
        var proposal = Proposal() with
        {
            Name = "Updated cave",
            Narrative = "Updated narrative",
            Entrances = [Entrance("entrance01", "Main", 110), Entrance("entrance02", "New", 50)],
            Files = [
                new ProposalFileIntent("file000001", ProposalFileDisposition.RetainPublished,
                    "filetype01", "Renamed file", ".pdf", "Survey"),
                new ProposalFileIntent("file000002", ProposalFileDisposition.PublishStaged,
                    "filetype01", "New file", ".pdf", "Survey")
            ],
            LinePlots = [new CaveProposalLinePlotV1 { Id = "lineplot01", Name = "Traverse", GeoJson = ChangedGeoJson }]
        };

        var summary = CaveProposalChangeScopeSummary.Build(before, proposal);

        Assert.Equal(2, summary.CaveFields);
        Assert.Equal(new DiffEntitySummary(1, 0, 1, 2), summary.Entrances);
        Assert.Equal(new DiffEntitySummary(1, 0, 1, 2), summary.Files);
        Assert.Equal(new DiffEntitySummary(0, 0, 1, 1), summary.LinePlots);
        Assert.Equal(0, summary.OtherChanges);
    }

    [Fact]
    public void SummarizeDoesNotCountRetainedUnchangedAggregateState()
    {
        var summary = CaveProposalChangeScopeSummary.Build(Snapshot(), Proposal());

        Assert.Equal(0, summary.CaveFields);
        Assert.Equal(0, summary.Entrances.Total);
        Assert.Equal(0, summary.Files.Total);
        Assert.Equal(0, summary.LinePlots.Total);
        Assert.Equal(0, summary.OtherChanges);
    }


    [Fact]
    public void SummarizeCountsStructuredFieldsRatherThanIndividualTagValues()
    {
        var proposal = Proposal() with
        {
            StateId = "state00002",
            StateNameAtRevision = "Tennessee",
            StateAbbreviationAtRevision = "TN",
            Tags = [
                new SnapshotTagReference(SnapshotTagRole.Geology, "geology001", "Limestone"),
                new SnapshotTagReference(SnapshotTagRole.Geology, "geology002", "Dolomite")
            ]
        };

        var summary = CaveProposalChangeScopeSummary.Build(Snapshot(), proposal);

        Assert.Equal(2, summary.CaveFields); // State + Geology, not one field per geology value.
        Assert.Equal(0, summary.Entrances.Total);
        Assert.Equal(0, summary.Files.Total);
        Assert.Equal(0, summary.LinePlots.Total);
    }

    private static CavePublishedSnapshotV1 Snapshot() => new()
    {
        CaveId = "cave000001", AccountId = "account001", Name = "Original cave", Narrative = "Original narrative",
        State = new SnapshotReference("state00001", "Kentucky", null, "KY"),
        County = new SnapshotReference("county0001", "Test County", "KX"), CountyNumber = 3,
        Entrances = [new CaveEntranceSnapshotV1 { Id = "entrance01", Name = "Main", IsPrimary = true,
            Latitude = 35, Longitude = -86, Elevation = 100, LocationQualityTagId = "quality001",
            LocationQualityNameAtRevision = "LiDAR" }],
        Files = [new CaveFileSnapshotV1 { Id = "file000001", FileTypeTagId = "filetype01",
            FileTypeNameAtRevision = "Survey", Name = "Original file", Extension = ".pdf" }],
        LinePlots = [new CaveLinePlotSnapshotV1 { Id = "lineplot01", Name = "Traverse",
            ContentHash = CaveJsonContent.NormalizeAndHash(BaseGeoJson).ContentHash }]
    };

    private static CaveProposalSnapshotV1 Proposal() => new()
    {
        CaveId = "cave000001", AccountId = "account001", Name = "Original cave", Narrative = "Original narrative",
        StateId = "state00001", StateNameAtRevision = "Kentucky", StateAbbreviationAtRevision = "KY",
        CountyId = "county0001", CountyNameAtRevision = "Test County", CountyDisplayIdAtRevision = "KX",
        CountyNumberIntent = CountyNumberIntent.AutomaticNext,
        Entrances = [Entrance("entrance01", "Main", 100)],
        Files = [new ProposalFileIntent("file000001", ProposalFileDisposition.RetainPublished,
            "filetype01", "Original file", ".pdf", "Survey")],
        LinePlots = [new CaveProposalLinePlotV1 { Id = "lineplot01", Name = "Traverse", GeoJson = BaseGeoJson }]
    };

    private static CaveProposalEntranceV1 Entrance(string id, string name, double elevation) => new()
    {
        EntranceId = id, Name = name, IsPrimary = id == "entrance01", Latitude = 35, Longitude = -86,
        Elevation = elevation, LocationQualityTagId = "quality001", LocationQualityNameAtRevision = "LiDAR"
    };

    private const string BaseGeoJson = "{\"type\":\"FeatureCollection\",\"features\":[]}";
    private const string ChangedGeoJson = "{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"properties\":{},\"geometry\":{\"type\":\"LineString\",\"coordinates\":[[0,0],[1,1]]}}]}";
}
