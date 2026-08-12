using Planarian.Model.Database.Revisions;
using Planarian.Modules.Caves.Models;
using Xunit;

namespace Planarian.Tests.Unit.Caves.Revisions;

public sealed class CaveProposalCountyNumberPresentationTests
{
    [Theory]
    [InlineData(CountyNumberIntent.Manual, "other-county", 123, CaveProposalCountyNumberMode.Manual, 123)]
    [InlineData(CountyNumberIntent.FirstAvailable, "base-county", null,
        CaveProposalCountyNumberMode.PreserveExisting, 47)]
    [InlineData(CountyNumberIntent.AutomaticNext, "base-county", null,
        CaveProposalCountyNumberMode.PreserveExisting, 47)]
    [InlineData(CountyNumberIntent.FirstAvailable, "other-county", null,
        CaveProposalCountyNumberMode.FirstAvailable, null)]
    [InlineData(CountyNumberIntent.AutomaticNext, "other-county", null,
        CaveProposalCountyNumberMode.AutomaticNext, null)]
    public void ResolvesProposalIntentRelativeToItsBaseCounty(CountyNumberIntent intent, string proposedCountyId,
        int? requestedNumber, CaveProposalCountyNumberMode expectedMode, int? expectedNumber)
    {
        var proposal = new CaveProposalSnapshotV1
        {
            CaveId = "cave", AccountId = "account", Name = "Proposal", CountyId = proposedCountyId,
            CountyNumberIntent = intent, RequestedCountyNumber = requestedNumber
        };
        var baseSnapshot = new CavePublishedSnapshotV1
        {
            CaveId = "cave", AccountId = "account", Name = "Published",
            State = new SnapshotReference("state", "State"),
            County = new SnapshotReference("base-county", "County"), CountyNumber = 47
        };

        var result = CaveProposalCountyNumberPresentation.Resolve(proposal, baseSnapshot);

        Assert.Equal(expectedMode, result.Mode);
        Assert.Equal(expectedNumber, result.Number);
    }
}
