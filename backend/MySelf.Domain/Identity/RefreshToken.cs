namespace MySelf.Domain.Identity;

/// <summary>
/// A single issued refresh token (docs/05: "rotating refresh token in HttpOnly cookie").
/// Only the SHA-256 hash of the raw value is stored — the same reasoning as password
/// hashing: a database leak alone must not hand out a live session. UserId is a plain Guid
/// (not a navigation property to ApplicationUser) because ApplicationUser is an
/// Infrastructure/Identity type and Domain does not reference Infrastructure.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// True when issued from a "trust this device" request — the cookie carrying this
    /// token got a persistent Expires (survives a browser close). False means it was
    /// issued as a browser-session cookie (no Expires), so the browser drops it once
    /// closed. Read back on /refresh so a rotated replacement keeps the same class.
    /// </summary>
    public bool IsPersistent { get; set; }
}
