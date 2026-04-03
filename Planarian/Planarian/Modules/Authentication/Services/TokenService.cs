using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.String;
using Planarian.Library.Options;
using Planarian.Modules.Authentication.Models;

namespace Planarian.Modules.Authentication.Services;

public class TokenService
{
    public static readonly string UserIdClaimType = nameof(UserToken.Id).ToCamelCase();
    private readonly AuthOptions _authOptions;

    public TokenService(AuthOptions authOptions)
    {
        _authOptions = authOptions;
    }

    public string BuildToken(UserToken user)
    {
        var claims = new List<Claim>()
        {
            new(ClaimTypes.Name, user.FullName),
            new(UserIdClaimType, user.Id)
        };

        if (!string.IsNullOrWhiteSpace(user.CurrentAccountId))
            claims.Add(new Claim(nameof(user.CurrentAccountId).ToCamelCase(), user.CurrentAccountId));

        return BuildToken(
            _authOptions.JwtSecret,
            claims,
            _authOptions.JwtExpiryDurationSeconds);
    }

    public bool IsTokenValid(string token)
    {
        try
        {
            ValidateToken(token, _authOptions.JwtSecret);
        }
        catch
        {
            return false;
        }

        return true;
    }

    public string GetUserIdFromToken(string token)
    {
        if (!IsTokenValid(token))
            throw new SecurityTokenException("Invalid token");

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);
        return jwtToken.Claims.First(claim => claim.Type == UserIdClaimType).Value;
    }

    public string? GetUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirst(UserIdClaimType)?.Value;
    }

    private string BuildToken(string secret, IEnumerable<Claim> claims, double expiryDurationSeconds)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw ApiExceptionDictionary.InternalServerError(
                "JWT signing secret is not configured.");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);
        var tokenDescriptor = new JwtSecurityToken(
            _authOptions.JwtIssuer,
            _authOptions.JwtIssuer,
            claims,
            expires: DateTime.UtcNow.AddSeconds(expiryDurationSeconds),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }

    private JwtSecurityToken ValidateToken(string token, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw ApiExceptionDictionary.InternalServerError(
                "JWT signing secret is not configured.");
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.ValidateToken(token,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidIssuer = _authOptions.JwtIssuer,
                ValidAudience = _authOptions.JwtIssuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ClockSkew = TimeSpan.Zero
            }, out _);

        return tokenHandler.ReadJwtToken(token);
    }
}
