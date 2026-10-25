using Planarian.Model.Database.Entities.RidgeWalker;
using Xunit;

namespace Planarian.Tests.Unit.Caves.Revisions;

public sealed class CaveChangeRequestStatePolicyTests
{
    public static TheoryData<CaveChangeRequestStatus, bool, bool, bool, bool, bool> StateCases => new()
    {
        { CaveChangeRequestStatus.Pending, true, true, false, true, true },
        { CaveChangeRequestStatus.ChangesRequested, true, false, true, true, true },
        { CaveChangeRequestStatus.Approved, false, false, false, false, false },
        { CaveChangeRequestStatus.Rejected, false, false, false, false, false },
    };

    [Theory]
    [MemberData(nameof(StateCases))]
    public void StatePolicyDefinesEveryWorkflowCapability(
        CaveChangeRequestStatus status,
        bool isOpen,
        bool isAwaitingReviewer,
        bool isAwaitingAuthor,
        bool canReceiveProposalVersions,
        bool blocksHardDelete)
    {
        Assert.Equal(isOpen, CaveChangeRequestStatePolicy.IsOpen(status));
        Assert.Equal(isAwaitingReviewer, CaveChangeRequestStatePolicy.IsAwaitingReviewer(status));
        Assert.Equal(isAwaitingAuthor, CaveChangeRequestStatePolicy.IsAwaitingAuthor(status));
        Assert.Equal(canReceiveProposalVersions, CaveChangeRequestStatePolicy.CanReceiveProposalVersions(status));
        Assert.Equal(isAwaitingReviewer, CaveChangeRequestStatePolicy.CanBePublished(status));
        Assert.Equal(blocksHardDelete, CaveChangeRequestStatePolicy.BlocksHardDelete(status));
    }

    [Fact]
    public void StateMatrixMustBeUpdatedWhenAWorkflowStatusIsAdded()
    {
        var covered = StateCases.Select(row => (CaveChangeRequestStatus)row[0]).Order().ToArray();
        var defined = Enum.GetValues<CaveChangeRequestStatus>().Order().ToArray();

        Assert.Equal(defined, covered);
    }
}
