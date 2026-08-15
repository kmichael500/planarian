using System.Text.Json;
using Planarian.Shared.Email.Models;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class MessageLogSubstitutionSerializerTests
{
    [Theory]
    [InlineData("https://app.example.com/confirm-email?code=SECRET", "https://app.example.com/confirm-email")]
    [InlineData("https://app.example.com/reset-password?code=SECRET#fragment", "https://app.example.com/reset-password")]
    [InlineData("https://app.example.com/user/invitations/SECRET", "https://app.example.com/user/invitations/[redacted]")]
    [InlineData("https://app.example.com/user/invitations/SECRET?x=1#fragment", "https://app.example.com/user/invitations/[redacted]")]
    [InlineData("/confirm-email?code=SECRET", "/confirm-email")]
    [InlineData("/user/invitations/SECRET?x=1", "/user/invitations/[redacted]")]
    public void CredentialBearingButtonUrlsAreSanitized(string input, string expected)
    {
        Assert.Equal(expected, MessageLogSubstitutionSerializer.SanitizeButtonUrl(input));
    }

    [Theory]
    [InlineData("/user/invitations", "/user/invitations")]
    [InlineData("/user/invitations/", "/user/invitations/")]
    [InlineData("/user/invitations-extra/SECRET", "/user/invitations-extra/SECRET")]
    public void InvitationRedactionDoesNotMatchAdjacentOrMissingCredentialSegments(string input, string expected)
    {
        Assert.Equal(expected, MessageLogSubstitutionSerializer.SanitizeButtonUrl(input));
    }

    [Fact]
    public void SerializePreservesUsefulJsonWithoutMutatingSendSubstitutions()
    {
        var substitutions = new Dictionary<string, object>
        {
            ["header"] = "Confirm your email address",
            ["buttonText"] = "Confirm Email",
            ["buttonUrl"] = "https://app.example.com/confirm-email?code=SECRET",
            ["websiteUrl"] = "https://app.example.com"
        };

        var serialized = MessageLogSubstitutionSerializer.Serialize(substitutions);
        using var document = JsonDocument.Parse(serialized);

        Assert.Equal("Confirm your email address", document.RootElement.GetProperty("header").GetString());
        Assert.Equal("Confirm Email", document.RootElement.GetProperty("buttonText").GetString());
        Assert.Equal("https://app.example.com/confirm-email", document.RootElement.GetProperty("buttonUrl").GetString());
        Assert.Equal("https://app.example.com", document.RootElement.GetProperty("websiteUrl").GetString());
        Assert.Equal("https://app.example.com/confirm-email?code=SECRET", substitutions["buttonUrl"]);
    }
}
