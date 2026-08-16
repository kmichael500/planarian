using System.Text.Json.Nodes;
using System.Text.Json;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Revisions;
using Xunit;

namespace Planarian.Tests.Unit.Caves.Revisions;

public sealed class CaveRevisionV1ContractTests
{
    [Fact]
    public void PublishedSnapshotV1FixtureIsTheStructuralPersistedContract()
    {
        var json = Fixture("cave-published-snapshot-v1.json");
        var snapshot = CaveSnapshotJson.Deserialize(json, 1);

        Assert.Equal("cave-001", snapshot.CaveId);
        Assert.Equal(0, snapshot.DepthFeet);
        Assert.Null(snapshot.MaxPitDepthFeet);
        Assert.Contains(snapshot.Tags, tag => tag.Role == SnapshotTagRole.CaveReportedBy &&
            tag.TagTypeId == "person-alice" && tag.NameAtRevision == "Alice Reporter");
        var entrance = Assert.Single(snapshot.Entrances);
        Assert.Equal("Survey Grade", entrance.LocationQualityNameAtRevision);
        Assert.Contains(entrance.Tags, tag => tag.Role == SnapshotTagRole.EntranceReportedBy);
        Assert.Equal("Map", Assert.Single(snapshot.Files).FileTypeNameAtRevision);
        var linePlot = Assert.Single(snapshot.LinePlots);
        Assert.Equal("lineplot-main", linePlot.Id);
        Assert.Equal("Main Line Plot", linePlot.Name);
        Assert.Equal(64, linePlot.ContentHash.Length);
        AssertStructuralJson(json, CaveSnapshotJson.Serialize(snapshot));
        Assert.DoesNotContain("ReportedByUserId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ReportedByNameAtRevision", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProposalSnapshotV1FixtureIsTheStructuralPersistedContract()
    {
        var json = Fixture("cave-proposal-snapshot-v1.json");
        var proposal = CaveProposalJson.Deserialize(json, 1);

        Assert.Equal(CountyNumberIntent.Manual, proposal.CountyNumberIntent);
        Assert.Equal(48, proposal.RequestedCountyNumber);
        Assert.Contains(proposal.Files, file => file.Disposition == ProposalFileDisposition.RetainPublished);
        Assert.Contains(proposal.Files, file => file.Disposition == ProposalFileDisposition.RemovePublished);
        Assert.Contains(proposal.Files, file => file.Disposition == ProposalFileDisposition.PublishStaged);
        var linePlot = Assert.Single(proposal.LinePlots);
        Assert.Equal("lineplot-main", linePlot.Id);
        Assert.Equal("{\"features\":[],\"type\":\"FeatureCollection\"}", linePlot.GeoJson);
        Assert.Contains(proposal.NewPeopleTagIntents, tag =>
            tag is { Role: SnapshotTagRole.Cartographer, Name: "New Cartographer" });
        Assert.Contains(Assert.Single(proposal.Entrances).NewPeopleTagIntents, tag =>
            tag is { Role: SnapshotTagRole.EntranceReportedBy, Name: "New Entrance Reporter" });
        Assert.Equal(CaveAlternateNameNormalizer.Normalize(proposal.AlternateNames), proposal.AlternateNames);
        AssertStructuralJson(json, CaveProposalJson.Serialize(proposal));
        Assert.DoesNotContain("ReportedByUserId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ReportedByNameAtRevision", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersistedV1EnumsAreStringsAndRoundTripTheirExpectedValues()
    {
        var publishedJson = Fixture("cave-published-snapshot-v1.json");
        var proposalJson = Fixture("cave-proposal-snapshot-v1.json");

        Assert.Equal(JsonValueKind.String, JsonDocument.Parse(publishedJson).RootElement
            .GetProperty("tags")[0].GetProperty("role").ValueKind);
        var proposalRoot = JsonDocument.Parse(proposalJson).RootElement;
        Assert.Equal(JsonValueKind.String, proposalRoot.GetProperty("countyNumberIntent").ValueKind);
        Assert.Equal(JsonValueKind.String, proposalRoot.GetProperty("files")[0].GetProperty("disposition").ValueKind);
        Assert.Equal(CountyNumberIntent.Manual, CaveProposalJson.Deserialize(proposalJson, 1).CountyNumberIntent);
        Assert.Equal(SnapshotTagRole.CaveReportedBy,
            CaveSnapshotJson.Deserialize(publishedJson, 1).Tags[0].Role);
    }

    [Fact]
    public void PersistedV1ReadersRejectNumericEnumTokens()
    {
        Assert.Throws<JsonException>(() => CaveSnapshotJson.Deserialize(
            Fixture("cave-published-snapshot-v1.json").Replace("\"CaveReportedBy\"", "8"), 1));
        Assert.Throws<JsonException>(() => CaveProposalJson.Deserialize(
            Fixture("cave-proposal-snapshot-v1.json").Replace("\"Manual\"", "2"), 1));
    }

    [Fact]
    public void ProposalV1RejectsPeopleIntentsOutsideTheirAllowedScope()
    {
        var fixture = Fixture("cave-proposal-snapshot-v1.json");
        Assert.Throws<InvalidOperationException>(() => CaveProposalJson.Deserialize(fixture.Replace(
            "\"role\": \"Cartographer\", \"name\": \"New Cartographer\"",
            "\"role\": \"Biology\", \"name\": \"New Cartographer\""), 1));
        Assert.Throws<InvalidOperationException>(() => CaveProposalJson.Deserialize(fixture.Replace(
            "\"role\": \"EntranceReportedBy\", \"name\": \"New Entrance Reporter\"",
            "\"role\": \"CaveReportedBy\", \"name\": \"New Entrance Reporter\""), 1));
    }

    [Fact]
    public void ProposalV1RejectsOversizedPeopleIntentNames()
    {
        var fixture = Fixture("cave-proposal-snapshot-v1.json");

        Assert.Throws<InvalidOperationException>(() => CaveProposalJson.Deserialize(fixture.Replace(
            "New Cartographer", new string('p', PropertyLength.Name + 1)), 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void PublishedSnapshotReaderRejectsUnsupportedVersions(int schemaVersion) =>
        Assert.Throws<NotSupportedException>(() => CaveSnapshotJson.Deserialize("{}", schemaVersion));

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ProposalReaderRejectsUnsupportedVersions(int schemaVersion) =>
        Assert.Throws<NotSupportedException>(() => CaveProposalJson.Deserialize("{}", schemaVersion));

    [Fact]
    public void ReadersRejectRowAndPayloadSchemaVersionDisagreement()
    {
        Assert.Throws<InvalidOperationException>(() => CaveSnapshotJson.Deserialize(Fixture(
            "cave-published-snapshot-v1.json").Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"), 1));
        Assert.Throws<InvalidOperationException>(() => CaveProposalJson.Deserialize(Fixture(
            "cave-proposal-snapshot-v1.json").Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"), 1));
    }

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "Caves", "Revisions", name));

    private static void AssertStructuralJson(string expected, string actual) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)));
}
