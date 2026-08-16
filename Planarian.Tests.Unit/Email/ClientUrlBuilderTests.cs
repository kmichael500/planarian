using Planarian.Shared.Routing;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.Unit.Email;

public sealed class ClientUrlBuilderTests
{
    private readonly ClientUrlBuilder _builder = new(new StubClientRequestOrigin("https://app.example.com"));

    [Fact]
    public void InvitationUrlUsesSharedRoutePrefixAndEscapesCredentialSegment()
    {
        var url = _builder.BuildInvitationUrl("code/?#");

        Assert.Equal(
            $"https://app.example.com{ClientRoutes.Invitation.Get("code/?#")}",
            url);
    }

    [Fact]
    public void EmailConfirmationUrlUsesSharedRoutePathAndEscapesQueryValue()
    {
        var url = _builder.BuildEmailConfirmationUrl("code/?#");

        Assert.Equal($"https://app.example.com{ClientRoutes.EmailConfirmation.Get("code/?#")}", url);
    }

    [Fact]
    public void PasswordResetUrlUsesSharedRoutePathAndEscapesQueryValue()
    {
        var url = _builder.BuildPasswordResetUrl("code/?#");

        Assert.Equal($"https://app.example.com{ClientRoutes.PasswordReset.Get("code/?#")}", url);
    }

    private sealed class StubClientRequestOrigin(string origin) : IClientRequestOrigin
    {
        public string GetOrigin() => origin;
    }
}
