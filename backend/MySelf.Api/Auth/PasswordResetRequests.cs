namespace MySelf.Api.Auth;

public sealed record ForgotPasswordRequest(string? Email);

/// <summary>ResetLink is only populated in Development — see PasswordResetEndpoints.</summary>
public sealed record ForgotPasswordResponse(string Message, string? ResetLink);

public sealed record ResetPasswordRequest(string? Email, string? Token, string? NewPassword);

public sealed record ResetPasswordResponse(string Message);
