using System.Text.Json;
using Planarian.Modules.Account.Model;
using Planarian.Shared.Options;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Users;

public sealed class AccountAccessRequestSerializationTests
{
    [Theory]
    [InlineData("disable", AccountAccessChangeAction.Disable)]
    [InlineData("enable", AccountAccessChangeAction.Enable)]
    [InlineData("revoke", AccountAccessChangeAction.Revoke)]
    public void AccountAccessRequestAcceptsClientActionValues(string action, AccountAccessChangeAction expected)
    {
        var request = JsonSerializer.Deserialize<AccountAccessChangeRequest>(
            "{\"action\":\"" + action + "\",\"sendEmail\":false}", PlanarianJsonOptions.Default);

        Assert.NotNull(request);
        Assert.Equal(expected, request.Action);
        Assert.False(request.SendEmail);

        var serialized = JsonSerializer.Serialize(request, PlanarianJsonOptions.Default);
        using var document = JsonDocument.Parse(serialized);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("action").ValueKind);
    }
}
