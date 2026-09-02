namespace MySelf.Infrastructure.Identity;

/// <summary>
/// Bound from the "Jwt" configuration section. Issuer/Audience/AccessTokenMinutes are not
/// secret and live in appsettings.json; Key is a secret and is set only via Jwt__Key in
/// .env (see Program.cs's fail-fast check, mirroring the DefaultConnection pattern).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Key { get; set; }
    public string Issuer { get; set; } = "myself-app";
    public string Audience { get; set; } = "myself-app-client";
    public int AccessTokenMinutes { get; set; } = 15;
}
