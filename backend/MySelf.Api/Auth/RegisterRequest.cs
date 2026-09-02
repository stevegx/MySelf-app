namespace MySelf.Api.Auth;

public sealed record RegisterRequest(string? Username, string? Email, string? Password, bool TrustThisDevice = false);
