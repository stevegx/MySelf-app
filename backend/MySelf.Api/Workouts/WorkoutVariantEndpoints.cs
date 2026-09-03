using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Workouts;
using MySelf.Infrastructure.Persistence;
using static MySelf.Api.Workouts.ProgramEndpoints;

namespace MySelf.Api.Workouts;

/// <summary>
/// Reading and rewriting a single variant's contents (docs/04 §12
/// <c>PUT /workout-variants/{id}</c>). The PUT takes the <em>whole</em> desired state —
/// exercises, their set prescriptions, and superset groupings — and replaces what's stored,
/// inside one transaction. That keeps reorder / add / remove / regroup a single, atomic
/// operation instead of a dozen fiddly endpoints.
/// </summary>
public static class WorkoutVariantEndpoints
{
    public static IEndpointRouteBuilder MapWorkoutVariantEndpoints(this IEndpointRouteBuilder app)
    {
        var variants = app.MapGroup("/api/v1/workout-variants")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimiting.WritePolicy);

        variants.MapGet("/{id:guid}", GetAsync).WithName("GetWorkoutVariant");
        variants.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateWorkoutVariant");
        variants.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteWorkoutVariant");

        return app;
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var variant = await db.OwnedVariants(userId)
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new VariantDetail(
                v.Id,
                v.Name,
                v.SortOrder,
                v.EstimatedDurationMinutes,
                v.Group.Program.RowVersion,
                v.Exercises
                    .OrderBy(e => e.SortOrder)
                    .Select(e => new VariantExerciseDetail(
                        e.Id,
                        e.ExerciseId,
                        e.Exercise.Name,
                        e.SortOrder,
                        e.SupersetGroupId,
                        e.SupersetMemberOrder,
                        e.RestSeconds,
                        e.Notes,
                        e.Sets
                            .OrderBy(s => s.SortOrder)
                            .Select(s => new SetPrescriptionDetail(
                                s.Id,
                                s.SortOrder,
                                s.Kind.ToString(),
                                s.IsAmrap,
                                s.TargetToFailure,
                                s.TargetRepsMin,
                                s.TargetRepsMax,
                                s.TargetWeightKg,
                                s.TargetRir))
                            .ToList()))
                    .ToList(),
                v.Supersets
                    .OrderBy(s => s.SortOrder)
                    .Select(s => new SupersetDetail(s.Id, s.SortOrder, s.RestAfterRoundSeconds))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        return variant is null ? Results.NotFound() : Results.Ok(variant);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateVariantRequest request,
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var variant = await db.OwnedVariants(userId)
            .Include(v => v.Group)
            .Include(v => v.Exercises).ThenInclude(e => e.Sets)
            .Include(v => v.Supersets)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        if (variant is null)
        {
            return Results.NotFound();
        }

        // The concurrency token lives on the program (docs/04). Load it tracked so the save
        // can guard on — and bump — its xmin: any edit in the tree moves the program version.
        var program = await db.WorkoutPrograms.FirstAsync(p => p.Id == variant.Group.ProgramId, ct);

        var exercises = request.Exercises ?? [];
        var supersets = request.Supersets ?? [];

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
            {
                return Validation("name", "Enter a variant name (1–80 characters).");
            }

            variant.Name = name;
        }

        if (request.EstimatedDurationMinutes is { } dur &&
            (dur < 0 || dur > WorkoutLimits.MaxEstimatedDurationMinutes))
        {
            return Validation("estimatedDurationMinutes",
                $"Estimated duration must be between 0 and {WorkoutLimits.MaxEstimatedDurationMinutes} minutes.");
        }

        if (exercises.Count > WorkoutLimits.MaxExercisesPerVariant)
        {
            return Validation("exercises", $"A variant can hold at most {WorkoutLimits.MaxExercisesPerVariant} exercises.");
        }

        if (supersets.Count > WorkoutLimits.MaxSupersetsPerVariant)
        {
            return Validation("supersets", $"A variant can hold at most {WorkoutLimits.MaxSupersetsPerVariant} supersets.");
        }

        foreach (var e in exercises)
        {
            if (e.SortOrder < 0 || e.SortOrder > WorkoutLimits.MaxSortOrder)
            {
                return Validation("exercises", "Exercise sort order is out of range.");
            }

            if (e.SupersetMemberOrder < 0 || e.SupersetMemberOrder > WorkoutLimits.MaxSupersetMemberOrder)
            {
                return Validation("exercises", "Superset member order is out of range.");
            }

            if (e.RestSeconds is { } rest && (rest < 0 || rest > WorkoutLimits.MaxRestSeconds))
            {
                return Validation("exercises", $"Rest must be between 0 and {WorkoutLimits.MaxRestSeconds} seconds.");
            }
        }

        foreach (var s in supersets)
        {
            if (s.SortOrder < 0 || s.SortOrder > WorkoutLimits.MaxSetSortOrder)
            {
                return Validation("supersets", "Superset sort order is out of range.");
            }
        }

        // Every referenced exercise must exist in the catalogue.
        var referencedIds = exercises.Select(e => e.ExerciseId).Distinct().ToList();
        var knownIds = await db.Exercises
            .Where(e => referencedIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(ct);
        if (knownIds.Count != referencedIds.Count)
        {
            return Validation("exercises", "One or more exercises are not in the catalogue.");
        }

        // Superset refs: every ref an exercise points at must be declared, and used by ≥2
        // exercises (locked decision #22 — a superset groups two or more).
        var declaredRefs = supersets.Select(s => s.Ref).ToHashSet(StringComparer.Ordinal);
        var refUsage = exercises
            .Where(e => e.SupersetRef is not null)
            .GroupBy(e => e.SupersetRef!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var usedRef in refUsage.Keys)
        {
            if (!declaredRefs.Contains(usedRef))
            {
                return Validation("supersets", $"Exercise references an undeclared superset '{usedRef}'.");
            }
        }

        if (refUsage.Values.Any(count => count < 2))
        {
            return Validation("supersets", "A superset needs at least two exercises.");
        }

        foreach (var e in exercises)
        {
            if ((e.Sets?.Count ?? 0) > WorkoutLimits.MaxSetsPerExercise)
            {
                return Validation("sets", $"An exercise can hold at most {WorkoutLimits.MaxSetsPerExercise} sets.");
            }

            foreach (var s in e.Sets ?? [])
            {
                if (s.Kind is not null && !Enum.TryParse<SetKind>(s.Kind, ignoreCase: true, out _))
                {
                    return Validation("sets", "Set kind must be Standard or Drop.");
                }

                if (s.SortOrder < 0 || s.SortOrder > WorkoutLimits.MaxSetSortOrder)
                {
                    return Validation("sets", "Set sort order is out of range.");
                }

                if (s.TargetRepsMin is < 0 || s.TargetRepsMax is < 0 ||
                    (s.TargetRepsMin is { } min && s.TargetRepsMax is { } max && min > max))
                {
                    return Validation("sets", "Rep range is invalid.");
                }

                if (s.TargetWeightKg is < 0 or > 2000)
                {
                    return Validation("sets", "Target weight is out of range.");
                }

                if (s.TargetRir is { } rir && (rir < 0 || rir > WorkoutLimits.MaxTargetRir))
                {
                    return Validation("sets", $"Reps in reserve must be between 0 and {WorkoutLimits.MaxTargetRir}.");
                }
            }
        }

        // --- replace children, all in this one change set / transaction ---
        db.SetPrescriptions.RemoveRange(variant.Exercises.SelectMany(e => e.Sets));
        db.VariantExercises.RemoveRange(variant.Exercises);
        db.SupersetGroups.RemoveRange(variant.Supersets);

        var refToId = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var s in supersets)
        {
            var groupEntity = new SupersetGroup
            {
                Id = Guid.NewGuid(),
                VariantId = variant.Id,
                SortOrder = s.SortOrder,
                RestAfterRoundSeconds = Math.Clamp(s.RestAfterRoundSeconds, 0, 3600),
            };
            db.SupersetGroups.Add(groupEntity);
            refToId[s.Ref] = groupEntity.Id;
        }

        foreach (var e in exercises)
        {
            var variantExercise = new VariantExercise
            {
                Id = Guid.NewGuid(),
                VariantId = variant.Id,
                ExerciseId = e.ExerciseId,
                SortOrder = e.SortOrder,
                SupersetGroupId = e.SupersetRef is { } r ? refToId[r] : null,
                SupersetMemberOrder = e.SupersetMemberOrder,
                RestSeconds = e.RestSeconds,
                Notes = Trimmed(e.Notes),
            };

            foreach (var s in e.Sets ?? [])
            {
                variantExercise.Sets.Add(new SetPrescription
                {
                    Id = Guid.NewGuid(),
                    SortOrder = s.SortOrder,
                    Kind = Enum.TryParse<SetKind>(s.Kind, ignoreCase: true, out var kind) ? kind : SetKind.Standard,
                    IsAmrap = s.IsAmrap,
                    TargetToFailure = s.TargetToFailure,
                    TargetRepsMin = s.TargetRepsMin,
                    TargetRepsMax = s.TargetRepsMax,
                    TargetWeightKg = s.TargetWeightKg,
                    TargetRir = s.TargetRir,
                });
            }

            db.VariantExercises.Add(variantExercise);
        }

        variant.EstimatedDurationMinutes = request.EstimatedDurationMinutes;

        if (!await TrySaveWithRowVersionAsync(db, program, request.RowVersion, ct))
        {
            return StaleWrite();
        }

        return await GetAsync(id, http, db, ct);
    }

    private static async Task<IResult> DeleteAsync(Guid id, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var variant = await db.OwnedVariants(userId).FirstOrDefaultAsync(v => v.Id == id, ct);
        if (variant is null)
        {
            return Results.NotFound();
        }

        db.WorkoutVariants.Remove(variant);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
