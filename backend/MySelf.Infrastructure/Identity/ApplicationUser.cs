using Microsoft.AspNetCore.Identity;

namespace MySelf.Infrastructure.Identity;

/// <summary>
/// Identity's user row: credentials, email, security stamp. This is deliberately a thin
/// Identity concern — the business-facing profile (docs/04 UserProfile/UserGoal: date of
/// birth, height, units, goals...) lives in separate Domain entities added in the
/// onboarding slice, keyed by this user's Id.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>;
