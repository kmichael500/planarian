using Planarian.Model.Shared;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class MessageDeliveryStatusPolicyTests
{
    public static TheoryData<MessageDeliveryStatus, MessageDeliveryStatus, bool> ProviderTransitions => new()
    {
        { MessageDeliveryStatus.Accepted, MessageDeliveryStatus.Accepted, true },
        { MessageDeliveryStatus.Accepted, MessageDeliveryStatus.TemporaryFailed, true },
        { MessageDeliveryStatus.Accepted, MessageDeliveryStatus.Delivered, true },
        { MessageDeliveryStatus.Accepted, MessageDeliveryStatus.PermanentFailed, true },
        { MessageDeliveryStatus.TemporaryFailed, MessageDeliveryStatus.Accepted, false },
        { MessageDeliveryStatus.TemporaryFailed, MessageDeliveryStatus.TemporaryFailed, true },
        { MessageDeliveryStatus.TemporaryFailed, MessageDeliveryStatus.Delivered, true },
        { MessageDeliveryStatus.TemporaryFailed, MessageDeliveryStatus.PermanentFailed, true },
        { MessageDeliveryStatus.Delivered, MessageDeliveryStatus.Accepted, false },
        { MessageDeliveryStatus.Delivered, MessageDeliveryStatus.TemporaryFailed, false },
        { MessageDeliveryStatus.Delivered, MessageDeliveryStatus.Delivered, true },
        { MessageDeliveryStatus.Delivered, MessageDeliveryStatus.PermanentFailed, true },
        { MessageDeliveryStatus.PermanentFailed, MessageDeliveryStatus.Accepted, false },
        { MessageDeliveryStatus.PermanentFailed, MessageDeliveryStatus.TemporaryFailed, false },
        { MessageDeliveryStatus.PermanentFailed, MessageDeliveryStatus.Delivered, false },
        { MessageDeliveryStatus.PermanentFailed, MessageDeliveryStatus.PermanentFailed, true }
    };

    [Theory]
    [MemberData(nameof(ProviderTransitions))]
    public void ProviderTransitionsDoNotRegressDeliveryState(
        MessageDeliveryStatus current,
        MessageDeliveryStatus incoming,
        bool expected)
    {
        var timestamp = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(expected, MessageDeliveryStatusPolicy.ShouldApply(current, timestamp, incoming, timestamp));
    }

    public static TheoryData<MessageDeliveryStatus, MessageDeliveryStatus> LocalProviderTruthTransitions => new()
    {
        { MessageDeliveryStatus.Submitting, MessageDeliveryStatus.Accepted },
        { MessageDeliveryStatus.Submitting, MessageDeliveryStatus.TemporaryFailed },
        { MessageDeliveryStatus.Submitting, MessageDeliveryStatus.Delivered },
        { MessageDeliveryStatus.Submitting, MessageDeliveryStatus.PermanentFailed },
        { MessageDeliveryStatus.Submitted, MessageDeliveryStatus.Accepted },
        { MessageDeliveryStatus.Submitted, MessageDeliveryStatus.TemporaryFailed },
        { MessageDeliveryStatus.Submitted, MessageDeliveryStatus.Delivered },
        { MessageDeliveryStatus.Submitted, MessageDeliveryStatus.PermanentFailed },
        { MessageDeliveryStatus.SendFailed, MessageDeliveryStatus.Accepted },
        { MessageDeliveryStatus.SendFailed, MessageDeliveryStatus.TemporaryFailed },
        { MessageDeliveryStatus.SendFailed, MessageDeliveryStatus.Delivered },
        { MessageDeliveryStatus.SendFailed, MessageDeliveryStatus.PermanentFailed }
    };

    [Theory]
    [MemberData(nameof(LocalProviderTruthTransitions))]
    public void AnyProviderDeliveryTruthCanReplaceLocalSubmissionState(
        MessageDeliveryStatus current, MessageDeliveryStatus incoming)
    {
        var currentOn = new DateTime(2026, 8, 14, 12, 0, 1, DateTimeKind.Utc);

        Assert.True(MessageDeliveryStatusPolicy.ShouldApply(current, currentOn, incoming, currentOn.AddSeconds(-1)));
    }

    [Fact]
    public void MissingCachedStatusAcceptsProviderStatus()
    {
        var incomingOn = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(MessageDeliveryStatusPolicy.ShouldApply(
            null, null, MessageDeliveryStatus.Accepted, incomingOn));
    }

    [Fact]
    public void OlderProviderEventCannotOverwriteNewerProviderState()
    {
        var currentOn = new DateTime(2026, 8, 14, 12, 0, 1, DateTimeKind.Utc);

        Assert.False(MessageDeliveryStatusPolicy.ShouldApply(
            MessageDeliveryStatus.Delivered,
            currentOn,
            MessageDeliveryStatus.PermanentFailed,
            currentOn.AddSeconds(-1)));
    }
}
