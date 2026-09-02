namespace MySelf.Api.Auth;

/// <summary>Identifier is either a username or an email — see AuthEndpoints.LoginAsync.</summary>
public sealed record LoginRequest(string? Identifier, string? Password, bool TrustThisDevice = false);
