using Planarian.Model.Shared;
using Xunit;

namespace Planarian.Tests.Unit.Email;

public sealed class EmailDeliveryEnumContractTests
{
    [Fact]
    public void MessagePurposeNamesAreStableStringValues()
    {
        AssertEnumNames<MessagePurpose>(
            "Generic", "EmailConfirmation", "PasswordReset", "AccountInvitation", "PasswordChanged");
    }

    [Fact]
    public void MessageProviderNamesAreStableStringValues()
    {
        AssertEnumNames<MessageProvider>("Mailgun");
    }

    [Fact]
    public void MessageDeliveryStatusNamesAreStableStringValues()
    {
        AssertEnumNames<MessageDeliveryStatus>(
            "Submitting", "Submitted", "SendFailed", "Accepted", "TemporaryFailed", "Delivered", "PermanentFailed");
    }

    [Fact]
    public void MessageDeliveryEventTypeNamesAreStableStringValues()
    {
        AssertEnumNames<MessageDeliveryEventType>(
            "Unknown", "Accepted", "TemporaryFailed", "Delivered", "PermanentFailed",
            "Opened", "Clicked", "Unsubscribed", "Complained");
    }

    private static void AssertEnumNames<TEnum>(params string[] expected) where TEnum : struct, Enum
    {
        Assert.Equal(
            expected.OrderBy(value => value, StringComparer.Ordinal),
            Enum.GetNames<TEnum>().OrderBy(value => value, StringComparer.Ordinal));
    }
}
