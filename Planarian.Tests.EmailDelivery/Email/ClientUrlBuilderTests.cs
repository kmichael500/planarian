using Planarian.Shared.Routing;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class ClientUrlBuilderTests
{
    private readonly ClientUrlBuilder _builder = new(new StubClientRequestOrigin("https://app.example.com"));

    [Fact]
    public void InvitationUrlUsesSharedRoutePrefixAndEscapesCredentialSegment()
    {
        var url = _builder.BuildInvitationUrl("code/?#");

        Assert.Equal(
            $"https://app.example.com{UserInvitationRoutes.Client.Get("code/?#")}",
            url);
    }

    [Fact]
    public void EmailConfirmationUrlUsesSharedRoutePathAndEscapesQueryValue()
    {
        var url = _builder.BuildEmailConfirmationUrl("code/?#");

        Assert.Equal("https://app.example.com/confirm-email?code=code%2F%3F%23", url);
    }

    [Fact]
    public void PasswordResetUrlUsesSharedRoutePathAndEscapesQueryValue()
    {
        var url = _builder.BuildPasswordResetUrl("code/?#");

        Assert.Equal("https://app.example.com/reset-password?code=code%2F%3F%23", url);
    }

    private sealed class StubClientRequestOrigin(string origin) : IClientRequestOrigin
    {
        public string GetOrigin() => origin;
    }
}
