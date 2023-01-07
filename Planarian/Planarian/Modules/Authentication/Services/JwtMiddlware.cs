using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Planarian.Modules.Authentication.Services;

public class CustomJwtSecurityTokenHandler : ISecurityTokenValidator
{
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public CustomJwtSecurityTokenHandler()
    {
        _tokenHandler = new JwtSecurityTokenHandler();
    }
    
    public bool CanValidateToken => true;

    public int MaximumTokenSizeInBytes { get; set; } = TokenValidationParameters.DefaultMaximumTokenSizeInBytes;

    public bool CanReadToken(string securityToken)
    {
        return _tokenHandler.CanReadToken(securityToken);            
    }

    public ClaimsPrincipal ValidateToken(string securityToken, TokenValidationParameters validationParameters, out SecurityToken validatedToken)
    {
        //How to access HttpContext/IP address from here?

        var principal = _tokenHandler.ValidateToken(securityToken, validationParameters, out validatedToken);

        return principal;
    }
}