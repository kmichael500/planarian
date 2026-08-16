using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Planarian.Library.Options;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Services;
using Xunit;

namespace Planarian.Tests.Unit.Authentication;

public sealed class SessionSecurityTests
{
    [Fact]
    public void NewTokensCarryTheCurrentSessionVersion()
    {
        var tokenService = CreateTokenService();
        var token = tokenService.BuildToken(new UserToken("Test User", "abcdefghij", null, 7));
        var principal = ReadPrincipal(token);

        Assert.Equal(7, TokenService.GetSessionVersion(principal));
    }

    [Fact]
    public void LegacyTokensWithoutSessionVersionRemainVersionZero()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(TokenService.UserIdClaimType, "abcdefghij")
        }));

        Assert.Equal(0, TokenService.GetSessionVersion(principal));
    }

    private static TokenService CreateTokenService() => new(new AuthOptions
    {
        JwtSecret = new string('s', 64),
        JwtIssuer = "planarian-tests",
        JwtExpiryDurationSeconds = 60
    });

    private static ClaimsPrincipal ReadPrincipal(string token)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims));
    }
}
