using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MySelf.Infrastructure.Identity;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>RawValue goes to the caller (as an HttpOnly cookie); Hash is what gets persisted.</summary>
public sealed record IssuedRefreshToken(string RawValue, string Hash, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues the short-lived JWT access token and the opaque refresh-token pair (docs/05).
/// This service only issues tokens — validating an incoming access token on protected
/// endpoints (JWT bearer authentication middleware, [Authorize]) is wired up in a later
/// slice once there is an endpoint that actually needs to be protected.
/// </summary>
public sealed class TokenService(IOptions<JwtOptions> jwtOptions, TimeProvider clock)
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public AccessToken CreateAccessToken(ApplicationUser user)
    {
        var options = jwtOptions.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(options.AccessTokenMinutes);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        ];

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public IssuedRefreshToken CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var expiresAt = clock.GetUtcNow().Add(RefreshTokenLifetime);
        return new IssuedRefreshToken(raw, Hash(raw), expiresAt);
    }

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
