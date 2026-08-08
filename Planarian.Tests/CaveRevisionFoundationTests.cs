using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Xunit;

namespace Planarian.Tests;

public class CaveRevisionFoundationTests
{
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
    public void ScopeFailsClosedWithoutAccount()
    {
        var user = new RequestUser(null!) { Id = "user-a", AccountId = null };
        Assert.Throws<InvalidOperationException>(() => AccountExecutionScope.Require(user));
    }
}
