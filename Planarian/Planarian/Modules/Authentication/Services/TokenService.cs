using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Azure;
using Microsoft.IdentityModel.Tokens;
using Planarian.Library.Extensions.String;
using Planarian.Library.Options;
using Planarian.Modules.Authentication.Models;

namespace Planarian.Modules.Authentication.Services;

public class TokenService
{
    private const double ExpiryDurationMinutes = 2880;
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
            new(nameof(user.Id).ToCamelCase(), user.Id),
        };

        if (!string.IsNullOrWhiteSpace(user.AccountId))
        {
            claims.Add(new Claim(nameof(user.AccountId).ToCamelCase(), user.AccountId));
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authOptions.JwtSecret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);
        var tokenDescriptor = new JwtSecurityToken(_authOptions.JwtIssuer, _authOptions.JwtIssuer, claims,
            expires: DateTime.UtcNow.AddMinutes(ExpiryDurationMinutes), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }

    public bool IsTokenValid(string token)
    {
        var key = _authOptions.JwtSecret;
        var issuer = _authOptions.JwtIssuer;

        var mySecret = Encoding.UTF8.GetBytes(key);
        var mySecurityKey = new SymmetricSecurityKey(mySecret);
        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            tokenHandler.ValidateToken(token,
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = issuer,
                    ValidAudience = issuer,
                    IssuerSigningKey = mySecurityKey
                }, out var validatedToken);
        }
        catch
        {
            return false;
        }

        return true;
    }

    public string GetUserIdFromToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);
        return jwtToken.Claims.First(claim => claim.Type == nameof(UserToken.Id).ToCamelCase()).Value;
    }
}