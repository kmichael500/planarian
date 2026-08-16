using System.Text.Json;
using Planarian.Shared.Email.Models;
using Xunit;

namespace Planarian.Tests.Unit.Email;

public sealed class MessageLogSubstitutionSerializerTests
{
    [Theory]
    [InlineData("https://app.example.com/confirm-email?code=SECRET", "https://app.example.com/confirm-email?code=[redacted]")]
    [InlineData("https://app.example.com/reset-password?code=SECRET#fragment", "https://app.example.com/reset-password?code=[redacted]")]
    [InlineData("https://app.example.com/confirm-email?source=email&code=SECRET&returnTo=%2Fsettings#fragment", "https://app.example.com/confirm-email?source=email&code=[redacted]&returnTo=%2Fsettings")]
    [InlineData("https://app.example.com/confirm-email?CODE=SECRET&source=email", "https://app.example.com/confirm-email?CODE=[redacted]&source=email")]
    [InlineData("https://app.example.com/user/invitations/SECRET", "https://app.example.com/user/invitations/[redacted]")]
    [InlineData("https://app.example.com/user/invitations/SECRET?x=1#fragment", "https://app.example.com/user/invitations/[redacted]?x=1")]
    [InlineData("/confirm-email?code=SECRET&source=email#fragment", "/confirm-email?code=[redacted]&source=email")]
    [InlineData("/user/invitations/SECRET?x=1#fragment", "/user/invitations/[redacted]?x=1")]
    public void CredentialBearingButtonUrlsAreSanitized(string input, string expected)
    {
        Assert.Equal(expected, MessageLogSubstitutionSerializer.SanitizeButtonUrl(input));
    }

    [Theory]
    [InlineData("/user/invitations", "/user/invitations")]
    [InlineData("/user/invitations/", "/user/invitations/")]
    [InlineData("/user/invitations-extra/SECRET?code=KEEP#fragment", "/user/invitations-extra/SECRET?code=KEEP")]
    [InlineData("/somewhere?code=KEEP&filter=active#fragment", "/somewhere?code=KEEP&filter=active")]
    [InlineData("https://app.example.com/somewhere?code=KEEP&filter=active#fragment", "https://app.example.com/somewhere?code=KEEP&filter=active")]
    public void UnrelatedRoutesPreserveQueryParametersWhileDroppingFragments(string input, string expected)
    {
        Assert.Equal(expected, MessageLogSubstitutionSerializer.SanitizeButtonUrl(input));
    }

    [Fact]
    public void InvitationRedactionPreservesSuffixAfterCredentialSegment()
    {
        Assert.Equal("/user/invitations/[redacted]/details?x=1",
            MessageLogSubstitutionSerializer.SanitizeButtonUrl("/user/invitations/SECRET/details?x=1#fragment"));
    }

    [Fact]
    public void SerializePreservesUsefulJsonWithoutMutatingSendSubstitutions()
    {
        var substitutions = new Dictionary<string, object>
        {
            ["header"] = "Confirm your email address",
            ["buttonText"] = "Confirm Email",
            ["buttonUrl"] = "https://app.example.com/confirm-email?code=SECRET&source=email",
            ["websiteUrl"] = "https://app.example.com"
        };

        var serialized = MessageLogSubstitutionSerializer.Serialize(substitutions);
        using var document = JsonDocument.Parse(serialized);

        Assert.Equal("Confirm your email address", document.RootElement.GetProperty("header").GetString());
        Assert.Equal("Confirm Email", document.RootElement.GetProperty("buttonText").GetString());
        Assert.Equal("https://app.example.com/confirm-email?code=[redacted]&source=email", document.RootElement.GetProperty("buttonUrl").GetString());
        Assert.Equal("https://app.example.com", document.RootElement.GetProperty("websiteUrl").GetString());
        Assert.Equal("https://app.example.com/confirm-email?code=SECRET&source=email", substitutions["buttonUrl"]);
    }
}
