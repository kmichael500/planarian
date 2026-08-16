using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Xunit;

namespace Planarian.Tests.Unit.Caves.Revisions;

public class CaveRevisionFoundationTests
{
    [Fact]
    public void ProposalJsonWithoutOptionalFilePresentationMetadataRemainsReadable()
    {
        const string json = """
            {"schemaVersion":1,"caveId":"c","accountId":"a","name":"Cave","stateId":"s","countyId":"co","files":[{"fileId":"f","disposition":"RetainPublished","fileTypeTagId":"report","displayName":"Survey"}]}
            """;

        var file = Assert.Single(CaveProposalJson.Deserialize(json, 1).Files);

        Assert.Null(file.FileName);
        Assert.Null(file.FileTypeName);
    }

    [Theory]
    [InlineData("survey.pdf", "Survey", "Survey Map", "Survey Map.pdf")]
    [InlineData("survey.pdf", "Survey", "Survey", "survey.pdf")]
    [InlineData("survey.pdf", "Survey", null, "survey.pdf")]
    [InlineData("survey", null, "Renamed", "Renamed")]
    public void EffectiveProposalFileNameMatchesPublicationSemantics(string existingFileName,
        string? existingDisplayName, string? proposedDisplayName, string expected) =>
        Assert.Equal(expected, CaveFileNamePolicy.GetEffectiveFileName(existingFileName,
            existingDisplayName, proposedDisplayName));

    [Fact]
    public void RoundTripUnchangedSnapshotIsSemanticNoOp()
    {
        var snapshot = new CavePublishedSnapshotV1
        {
            CaveId = "cave-a", AccountId = "account-a", Name = "Example",
            State = new SnapshotReference("state-a", "State", null, "ST"),
            County = new SnapshotReference("county-a", "County", "001"),
            Tags = [new SnapshotTagReference(SnapshotTagRole.Cartographer, "tag-a", "Alice")]
        };

        var roundTrip = CaveSnapshotJson.Deserialize(CaveSnapshotJson.Serialize(snapshot), 1);
        Assert.True(new CaveRevisionDiffService().IsSemanticEqual(snapshot, roundTrip));
    }

    [Fact]
    public void SameTagIdInDifferentRolesIsNotCollapsed()
    {
        var before = new CavePublishedSnapshotV1
        {
            CaveId = "c", AccountId = "a", Name = "n",
            State = new SnapshotReference("s", "S"), County = new SnapshotReference("co", "C"),
            Tags = [new SnapshotTagReference(SnapshotTagRole.Cartographer, "person", "Alice")]
        };
        var after = before with
        {
            Tags = [new SnapshotTagReference(SnapshotTagRole.CaveReportedBy, "person", "Alice")]
        };

        var diff = new CaveRevisionDiffService().Compare(before, after);
        Assert.Single(diff.AddedTags);
        Assert.Single(diff.RemovedTags);
    }

    [Fact]
    public void CaveReportedByPeopleTagAdditionsAndRemovalsRemainSemanticDifferences()
    {
        var before = new CavePublishedSnapshotV1
        {
            CaveId = "c", AccountId = "a", Name = "n",
            State = new SnapshotReference("s", "S"), County = new SnapshotReference("co", "C"),
            Tags = [new SnapshotTagReference(SnapshotTagRole.CaveReportedBy, "alice", "Alice")]
        };
        var after = before with
        {
            Tags = [new SnapshotTagReference(SnapshotTagRole.CaveReportedBy, "bob", "Bob")]
        };

        var diff = new CaveRevisionDiffService().Compare(before, after);

        Assert.Equal("Alice", Assert.Single(diff.RemovedTags).NameAtRevision);
        Assert.Equal("Bob", Assert.Single(diff.AddedTags).NameAtRevision);
    }

    [Fact]
    public void EquivalentEntranceTagsRemainEqualAfterRoundTrip()
    {
        var snapshot = new CavePublishedSnapshotV1
        {
            CaveId = "c", AccountId = "a", Name = "n",
            State = new SnapshotReference("s", "S"), County = new SnapshotReference("co", "C"),
            Entrances = [new CaveEntranceSnapshotV1
            {
                Id = "entrance", LocationQualityTagId = "quality", LocationQualityNameAtRevision = "Good",
                Tags = [new SnapshotTagReference(SnapshotTagRole.EntranceReportedBy, "person", "Alice")]
            }]
        };

        var roundTrip = CaveSnapshotJson.Deserialize(CaveSnapshotJson.Serialize(snapshot), 1);
        Assert.True(new CaveRevisionDiffService().IsSemanticEqual(snapshot, roundTrip));
    }

    [Fact]
    public void SameTagIdentityWithRenamedHistoricalLabelIsReferenceMetadataChange()
    {
        var r1 = new CavePublishedSnapshotV1
        {
            CaveId = "c", AccountId = "a", Name = "n", Narrative = "old",
            State = new SnapshotReference("s", "State"), County = new SnapshotReference("co", "County"),
            Tags = [new SnapshotTagReference(SnapshotTagRole.Geology, "g1", "Limestone")]
        };
        var r2 = r1 with { Narrative = "new", Tags = [new SnapshotTagReference(SnapshotTagRole.Geology, "g1", "Carbonate Limestone")] };

        var diff = new CaveRevisionDiffService().Compare(r1, r2);
        Assert.Equal(("old", "new"), diff.Scalars[nameof(CavePublishedSnapshotV1.Narrative)]);
        Assert.Empty(diff.AddedTags);
        Assert.Empty(diff.RemovedTags);
        Assert.Contains(diff.ReferenceMetadataChanges, change => change.StableId == "g1" &&
            change.PreviousValue == "Limestone" && change.CurrentValue == "Carbonate Limestone");
        Assert.False(new CaveRevisionDiffService().IsSemanticEqual(r1, r2));
    }

    [Fact]
    public void StateCountyEntranceAndFileLabelsAreReferenceMetadataChanges()
    {
        var before = new CavePublishedSnapshotV1
        {
            CaveId = "c", AccountId = "a", Name = "n",
            State = new SnapshotReference("s", "Georgia", null, "GA"),
            County = new SnapshotReference("co", "Marion", "MAR"),
            Entrances = [new CaveEntranceSnapshotV1 { Id = "e", LocationQualityTagId = "q", LocationQualityNameAtRevision = "Exact" }],
            Files = [new CaveFileSnapshotV1 { Id = "f", FileTypeTagId = "map", FileTypeNameAtRevision = "Map", FileName = "x" }]
        };
        var after = before with
        {
            State = new SnapshotReference("s", "State of Georgia", null, "GA"),
            County = new SnapshotReference("co", "Marion", "MA"),
            Entrances = [new CaveEntranceSnapshotV1 { Id = "e", LocationQualityTagId = "q", LocationQualityNameAtRevision = "Survey Grade" }],
            Files = [new CaveFileSnapshotV1 { Id = "f", FileTypeTagId = "map", FileTypeNameAtRevision = "Cave Map", FileName = "x" }]
        };

        var diff = new CaveRevisionDiffService().Compare(before, after);
        Assert.Empty(diff.Scalars);
        Assert.Contains("e", diff.ChangedEntrances);
        Assert.Contains("f", diff.ChangedFiles);
        Assert.Equal(4, diff.ReferenceMetadataChanges.Count);
    }

    [Fact]
    public void ScopeFailsClosedWithoutAccount()
    {
        var user = new RequestUser(null!) { Id = "user-a", AccountId = null };
        Assert.Throws<InvalidOperationException>(() => AccountExecutionScope.Require(user));
    }
}
