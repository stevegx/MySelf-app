using System.IdentityModel.Tokens.Jwt;

namespace MySelf.Api;

/// <summary>
/// Pulls the authenticated user's id off the JWT "sub" claim. "sub" survives as the literal
/// claim type because Program.cs sets
/// <c>JwtSecurityTokenHandler.DefaultMapInboundClaims = false</c>.
/// </summary>
public static class HttpUserExtensions
{
    public static bool TryGetUserId(this HttpContext http, out Guid userId) =>
        Guid.TryParse(http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out userId);
}
