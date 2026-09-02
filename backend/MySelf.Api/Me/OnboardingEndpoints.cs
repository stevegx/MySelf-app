using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Identity;
using MySelf.Domain.Nutrition;
using MySelf.Infrastructure.Persistence;

namespace MySelf.Api.Me;

/// <summary>
/// docs/04's <c>POST /me/onboarding/complete</c> and <c>GET /me/goals</c>. Completing
/// onboarding writes an effective-dated <see cref="UserGoal"/> (plus a
/// <see cref="NutritionEstimateSnapshot"/> when the target came from the estimator) and
/// stamps <see cref="UserProfile.OnboardingCompletedAt"/> — all in one transaction. It runs
/// once per account; a second call is a 409.
/// </summary>
public static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/me/onboarding/complete", CompleteAsync)
            .WithName("CompleteOnboarding")
            .WithSummary("Persist the chosen nutrition goal and mark onboarding done.")
            .RequireAuthorization();

        app.MapGet("/api/v1/me/goals", GetGoalsAsync)
            .WithName("GetGoals")
            .WithSummary("The signed-in user's nutrition goal history, newest first.")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> CompleteAsync(
        CompleteOnboardingRequest request,
        HttpContext httpContext,
        MySelfDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (!TryGetUserId(httpContext, out var userId))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Profile required",
                detail: "Save your profile (PUT /api/v1/me/profile) before completing onboarding.");
        }

        if (profile.OnboardingCompletedAt is not null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Onboarding already completed",
                detail: "Onboarding runs once. Change your goal from nutrition settings instead.");
        }

        var errors = new Dictionary<string, string[]>();
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var age = AgeCalculator.Years(profile.DateOfBirth, today);

        if (!TryParseEnum<GoalType>(request.GoalType, out var goalType))
        {
            errors["goalType"] = ["Choose Lose, Maintain, Gain or TrackOnly."];
        }

        if (request.Estimate is not null && request.ManualTarget is not null)
        {
            errors["mode"] = ["Send either an estimate or a manual target, not both."];
        }

        // targetWeightKg is only kept for Lose/Gain; ignored (nulled) otherwise rather than erroring.
        decimal? targetWeightKg = null;
        if (request.TargetWeightKg is { } tw)
        {
            if (tw is < 20m or > 500m)
            {
                errors["targetWeightKg"] = ["Enter a target weight between 20 and 500 kg."];
            }
            else if (goalType is GoalType.Lose or GoalType.Gain)
            {
                targetWeightKg = tw;
            }
        }

        UserGoal? goal = null;
        NutritionEstimateSnapshot? snapshot = null;

        if (request.Estimate is { } estimate)
        {
            goal = BuildEstimatedGoal(estimate, profile, goalType, age, errors, out snapshot, now);
        }
        else if (request.ManualTarget is { } manual)
        {
            goal = BuildManualGoal(manual, goalType, errors);
        }
        else
        {
            // Skipped nutrition, or a Track-only goal: a row with no targets, still recorded.
            goal = new UserGoal { Source = GoalSource.Manual };
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors, title: "Validation failed");
        }

        goal!.Id = Guid.NewGuid();
        goal.UserId = userId;
        goal.GoalType = goalType;
        goal.TargetWeightKg = targetWeightKg;
        goal.EffectiveFrom = now;
        goal.CreatedAt = now;
        db.UserGoals.Add(goal);

        if (snapshot is not null)
        {
            snapshot.Id = Guid.NewGuid();
            snapshot.UserId = userId;
            snapshot.UserGoalId = goal.Id;
            db.NutritionEstimateSnapshots.Add(snapshot);
        }

        profile.OnboardingCompletedAt = now;
        profile.UpdatedAt = now;

        // One SaveChanges => one transaction: the goal, the optional snapshot, and the
        // profile stamp all land together or not at all.
        await db.SaveChangesAsync(ct);

        return Results.Json(GoalSummary.From(goal), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetGoalsAsync(
        HttpContext httpContext,
        MySelfDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(httpContext, out var userId))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        var goals = await db.UserGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.EffectiveFrom)
            .ToListAsync(ct);

        return Results.Ok(goals.Select(GoalSummary.From));
    }

    private static UserGoal? BuildEstimatedGoal(
        OnboardingEstimateChoice estimate,
        UserProfile profile,
        GoalType goalType,
        int age,
        Dictionary<string, string[]> errors,
        out NutritionEstimateSnapshot? snapshot,
        DateTimeOffset now)
    {
        snapshot = null;

        if (goalType == GoalType.TrackOnly)
        {
            errors["goalType"] = ["A Track-only goal has no calculated target — omit the estimate to skip."];
        }

        if (profile.CalculationSex is null)
        {
            errors["estimate"] = ["Your profile has no calculation sex. Set one, or choose a manual target."];
        }

        if (!TryParseEnum<ActivityLevel>(estimate.ActivityLevel, out var activityLevel))
        {
            errors["activityLevel"] = ["Choose one of the listed activity levels."];
        }

        GoalPace? pace = null;
        if (goalType is GoalType.Lose or GoalType.Gain)
        {
            if (TryParseEnum<GoalPace>(estimate.Pace, out var parsedPace))
            {
                pace = parsedPace;
            }
            else
            {
                errors["pace"] = ["Choose Gentle or Standard for a Lose or Gain goal."];
            }
        }

        var weightKg = estimate.WeightKg ?? 0m;
        if (weightKg is < 20m or > 500m)
        {
            errors["weightKg"] = ["Enter a weight between 20 and 500 kg."];
        }

        var proteinFactor = estimate.ProteinFactor ?? CalorieEstimator.DefaultProteinFactor;
        if (proteinFactor < CalorieEstimator.MinProteinFactor || proteinFactor > CalorieEstimator.MaxProteinFactor)
        {
            errors["proteinFactor"] =
                [$"Protein must be between {CalorieEstimator.MinProteinFactor} and {CalorieEstimator.MaxProteinFactor} g/kg."];
        }

        var fatFactor = estimate.FatFactor ?? CalorieEstimator.DefaultFatFactor;
        if (fatFactor < CalorieEstimator.MinFatFactor || fatFactor > CalorieEstimator.MaxFatFactor)
        {
            errors["fatFactor"] =
                [$"Fat must be between {CalorieEstimator.MinFatFactor} and {CalorieEstimator.MaxFatFactor} g/kg."];
        }

        if (errors.Count > 0)
        {
            return null;
        }

        var result = CalorieEstimator.Estimate(new CalorieEstimateInput(
            AgeYears: age,
            CalculationSex: profile.CalculationSex!.Value,
            HeightCm: profile.HeightCm,
            WeightKg: weightKg,
            ActivityLevel: activityLevel,
            GoalType: goalType,
            Pace: pace,
            ProteinFactor: proteinFactor,
            FatFactor: fatFactor));

        if (!result.IsAvailable)
        {
            errors["estimate"] = [$"An estimated target is not available for this profile ({result.UnavailableReason})."];
            return null;
        }

        snapshot = new NutritionEstimateSnapshot
        {
            WeightKg = weightKg,
            HeightCm = profile.HeightCm,
            AgeYears = age,
            CalculationSex = profile.CalculationSex.Value,
            ActivityLevel = activityLevel,
            FormulaName = CalorieEstimator.FormulaName,
            FormulaVersion = CalorieEstimator.FormulaVersion,
            Bmr = result.Bmr!.Value,
            Tdee = result.MaintenanceCalories!.Value,
            SelectedAdjustment = result.GoalAdjustment!.Value,
            SuggestedTarget = result.SuggestedCalories!.Value,
            CalculatedAt = now,
        };

        return new UserGoal
        {
            Source = GoalSource.Estimated,
            CalorieTarget = result.SuggestedCalories,
            ProteinGrams = result.Macros!.ProteinGrams,
            CarbGrams = result.Macros.CarbGrams,
            FatGrams = result.Macros.FatGrams,
        };
    }

    private static UserGoal? BuildManualGoal(
        OnboardingManualTarget manual,
        GoalType goalType,
        Dictionary<string, string[]> errors)
    {
        if (goalType == GoalType.TrackOnly)
        {
            errors["manualTarget"] = ["A Track-only goal has no calorie target — omit the manual target to skip."];
        }

        if (manual.CalorieTarget is not { } calories || calories is < 500 or > 20000)
        {
            errors["calorieTarget"] = ["Enter a calorie target between 500 and 20000."];
        }

        foreach (var (field, grams) in new[]
                 {
                     ("proteinGrams", manual.ProteinGrams),
                     ("carbGrams", manual.CarbGrams),
                     ("fatGrams", manual.FatGrams),
                 })
        {
            if (grams is < 0 or > 2000)
            {
                errors[field] = ["Enter a value between 0 and 2000 g."];
            }
        }

        if (errors.Count > 0)
        {
            return null;
        }

        return new UserGoal
        {
            Source = GoalSource.Manual,
            CalorieTarget = manual.CalorieTarget,
            ProteinGrams = manual.ProteinGrams,
            CarbGrams = manual.CarbGrams,
            FatGrams = manual.FatGrams,
        };
    }

    private static bool TryGetUserId(HttpContext httpContext, out Guid userId)
    {
        var claim = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(claim, out userId);
    }

    private static bool TryParseEnum<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed))
        {
            return true;
        }

        parsed = default;
        return false;
    }
}
