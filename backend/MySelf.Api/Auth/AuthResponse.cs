namespace MySelf.Api.Auth;

/// <summary>Shared shape for the authenticated-user summary — register, login, and /me all return this.</summary>
public sealed record AuthUser(Guid Id, string Username, string Email);

public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, AuthUser User);
