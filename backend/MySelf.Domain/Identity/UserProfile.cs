namespace MySelf.Domain.Identity;

/// <summary>
/// The business-facing profile for a user (docs/04 §11): the personalization inputs collected
/// during onboarding. Exactly one row per user, keyed by the Identity user's Id — a missing
/// row means "this account has not started onboarding".
///
/// Like <see cref="RefreshToken"/>, there is no navigation property to ApplicationUser: that
/// type lives in Infrastructure/Identity and Domain does not reference Infrastructure. The
/// foreign key is declared in UserProfileConfiguration instead.
/// </summary>
public class UserProfile
{
    /// <summary>
    /// Primary key <em>and</em> the foreign key to AspNetUsers(Id). Sharing the key this way
    /// makes "at most one profile per user" a schema guarantee rather than something the code
    /// has to remember to check.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>Birth date only — no time, no zone. Age for the estimator is derived from this, never entered directly (docs/01 step 1).</summary>
    public DateOnly DateOfBirth { get; set; }

    /// <summary>Canonical height in centimetres. Stored as decimal (not float) per docs/04's "no binary float for persisted measurements".</summary>
    public decimal HeightCm { get; set; }

    /// <summary>Null when the user opted out of the calorie estimate (docs/01 step 1). Only the estimator reads it.</summary>
    public CalculationSex? CalculationSex { get; set; }

    public UnitSystem UnitSystem { get; set; }

    /// <summary>IANA timezone id, e.g. "Europe/Athens". Taken from the browser, not asked in the wizard.</summary>
    public string? Timezone { get; set; }

    /// <summary>BCP 47 locale, e.g. "en-GB". Taken from the browser.</summary>
    public string? Locale { get; set; }

    /// <summary>
    /// Stamped once, when the user finishes or explicitly skips onboarding via
    /// POST /me/onboarding/complete (a later slice). Null means the wizard is unfinished —
    /// which is what the frontend onboarding gate keys off.
    /// </summary>
    public DateTimeOffset? OnboardingCompletedAt { get; set; }

    /// <summary>
    /// UI preference: when adding an exercise whose primary muscle is outside a day's focus,
    /// show the "off-focus" nudge. Defaults to true; the user can turn it off from Settings
    /// (or via "don't warn me again" on the nudge itself).
    /// </summary>
    public bool WarnOffFocusExercises { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
