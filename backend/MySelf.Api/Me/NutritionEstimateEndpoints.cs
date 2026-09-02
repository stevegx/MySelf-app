using System.IdentityModel.Tokens.Jwt;
using MySelf.Domain.Identity;
using MySelf.Domain.Nutrition;

namespace MySelf.Api.Me;

/// <summary>
/// docs/04's <c>POST /me/nutrition-estimate</c>: returns the BMR → TDEE → adjustment →
/// suggested-target breakdown for a set of onboarding answers. Stateless — it reads nothing
/// and writes nothing (the wizard calls it repeatedly as answers change); persistence is a
/// later slice. All the maths lives in <see cref="CalorieEstimator"/>; this handler only
/// validates the request and shapes the response.
/// </summary>
public static class NutritionEstimateEndpoints
{
    /// <summary>Exact launch copy from docs/01. Travels in every response, available or not.</summary>
    private const string Disclaimer =
        "This calorie target is an estimate based on the information you provided. MySelf is a "
        + "tracking tool, not medical advice, and does not replace a doctor, registered dietitian, "
        + "or qualified trainer. Your actual needs may differ.";

    public static IEndpointRouteBuilder MapNutritionEstimateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/me/nutrition-estimate", Estimate)
            .WithName("NutritionEstimate")
            .WithSummary("Non-persisted BMR/TDEE/goal breakdown for onboarding answers.")
            .RequireAuthorization();

        return app;
    }

    private static IResult Estimate(
        NutritionEstimateRequest request,
        HttpContext httpContext,
        TimeProvider clock)
    {
        // A valid access token is all this needs — nothing is looked up or stored, so there is
        // no per-user data to scope. RequireAuthorization() already rejected anonymous calls;
        // this guard only covers a well-formed token with no sub claim.
        if (httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        var errors = new Dictionary<string, string[]>();
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        // Under-18 is NOT rejected here — it is a valid request that comes back with
        // nutritionEstimateAvailable = false. Only a missing or impossible date is a 400.
        var age = 0;
        if (request.DateOfBirth is not { } dob)
        {
            errors["dateOfBirth"] = ["Date of birth is required."];
        }
        else if (dob >= today)
        {
            errors["dateOfBirth"] = ["Date of birth must be in the past."];
        }
        else
        {
            age = AgeYears(dob, today);
            if (age > 120)
            {
                errors["dateOfBirth"] = ["Enter a valid date of birth."];
            }
        }

        if (!TryParseEnum<CalculationSex>(request.CalculationSex, out var calculationSex))
        {
            errors["calculationSex"] = ["Choose Male or Female. Skip the estimate entirely to omit it."];
        }

        var heightCm = request.HeightCm ?? 0m;
        if (heightCm is < 50m or > 260m)
        {
            errors["heightCm"] = ["Enter a height between 50 and 260 cm."];
        }

        var weightKg = request.WeightKg ?? 0m;
        if (weightKg is < 20m or > 500m)
        {
            errors["weightKg"] = ["Enter a weight between 20 and 500 kg."];
        }

        if (!TryParseEnum<ActivityLevel>(request.ActivityLevel, out var activityLevel))
        {
            errors["activityLevel"] = ["Choose one of the listed activity levels."];
        }

        if (!TryParseEnum<GoalType>(request.GoalType, out var goalType))
        {
            errors["goalType"] = ["Choose Lose, Maintain, Gain or TrackOnly."];
        }

        // Pace is only meaningful for Lose / Gain; for those it is required.
        GoalPace? pace = null;
        if (goalType is GoalType.Lose or GoalType.Gain)
        {
            if (TryParseEnum<GoalPace>(request.Pace, out var parsedPace))
            {
                pace = parsedPace;
            }
            else
            {
                errors["pace"] = ["Choose Gentle or Standard for a Lose or Gain goal."];
            }
        }

        var proteinFactor = request.ProteinFactor ?? CalorieEstimator.DefaultProteinFactor;
        if (proteinFactor < CalorieEstimator.MinProteinFactor || proteinFactor > CalorieEstimator.MaxProteinFactor)
        {
            errors["proteinFactor"] =
                [$"Protein must be between {CalorieEstimator.MinProteinFactor} and {CalorieEstimator.MaxProteinFactor} g/kg."];
        }

        var fatFactor = request.FatFactor ?? CalorieEstimator.DefaultFatFactor;
        if (fatFactor < CalorieEstimator.MinFatFactor || fatFactor > CalorieEstimator.MaxFatFactor)
        {
            errors["fatFactor"] =
                [$"Fat must be between {CalorieEstimator.MinFatFactor} and {CalorieEstimator.MaxFatFactor} g/kg."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors, title: "Validation failed");
        }

        var result = CalorieEstimator.Estimate(new CalorieEstimateInput(
            AgeYears: age,
            CalculationSex: calculationSex,
            HeightCm: heightCm,
            WeightKg: weightKg,
            ActivityLevel: activityLevel,
            GoalType: goalType,
            Pace: pace,
            ProteinFactor: proteinFactor,
            FatFactor: fatFactor));

        return Results.Ok(new NutritionEstimateResponse(
            FormulaName: CalorieEstimator.FormulaName,
            FormulaVersion: CalorieEstimator.FormulaVersion,
            NutritionEstimateAvailable: result.IsAvailable,
            UnavailableReason: result.UnavailableReason,
            Age: age,
            Bmr: result.Bmr,
            ActivityFactor: result.ActivityFactor,
            MaintenanceCalories: result.MaintenanceCalories,
            GoalAdjustment: result.GoalAdjustment,
            SuggestedCalories: result.SuggestedCalories,
            Macros: result.Macros,
            Warnings: result.Warnings,
            Disclaimer: Disclaimer));
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

    /// <summary>Whole years between the two dates. Mirrors the helper in MeEndpoints.</summary>
    private static int AgeYears(DateOnly dob, DateOnly on)
    {
        var age = on.Year - dob.Year;
        if (dob > on.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
