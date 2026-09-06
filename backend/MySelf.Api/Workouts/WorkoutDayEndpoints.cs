using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Workouts;
using MySelf.Infrastructure.Persistence;
using static MySelf.Api.Workouts.ProgramEndpoints;

namespace MySelf.Api.Workouts;

/// <summary>
/// Reading and rewriting a single day's contents (docs/04 §12 <c>PUT /workout-days/{id}</c>).
/// The PUT takes the <em>whole</em> desired state — exercises, their set prescriptions, and
/// superset groupings — and replaces what's stored, inside one transaction. That keeps
/// reorder / add / remove / regroup a single, atomic operation instead of a dozen fiddly
/// endpoints.
/// </summary>
public static class WorkoutDayEndpoints
{
    public static IEndpointRouteBuilder MapWorkoutDayEndpoints(this IEndpointRouteBuilder app)
    {
        var days = app.MapGroup("/api/v1/workout-days")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimiting.WritePolicy);

        days.MapGet("/{id:guid}", GetAsync).WithName("GetWorkoutDay");
        days.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateWorkoutDay");
        days.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteWorkoutDay");
        days.MapPost("/{id:guid}/exercises/bulk-copy", BulkCopyAsync).WithName("BulkCopyDayExercises");
        days.MapPost("/{id:guid}/exercises/bulk-move", BulkMoveAsync).WithName("BulkMoveDayExercises");

        return app;
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext http, MySelfDbContext db, CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var day = await db.OwnedDays(userId)
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new DayDetail(
                d.Id,
                d.Name,
                d.SortOrder,
                d.EstimatedDurationMinutes,
                d.Program.RowVersion,
                d.Exercises
                    .OrderBy(e => e.SortOrder)
                    .Select(e => new DayExerciseDetail(
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
                d.Supersets
                    .OrderBy(s => s.SortOrder)
                    .Select(s => new SupersetDetail(s.Id, s.SortOrder, s.RestAfterRoundSeconds))
                    .ToList(),
                d.FocusMuscleIds))
            .FirstOrDefaultAsync(ct);

        return day is null ? Results.NotFound() : Results.Ok(day);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateDayRequest request,
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var day = await db.OwnedDays(userId)
            .Include(d => d.Exercises).ThenInclude(e => e.Sets)
            .Include(d => d.Supersets)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (day is null)
        {
            return Results.NotFound();
        }

        // The concurrency token lives on the program (docs/04). Load it tracked so the save
        // can guard on — and bump — its xmin: any edit in the tree moves the program version.
        var program = await db.WorkoutPrograms.FirstAsync(p => p.Id == day.ProgramId, ct);

        var exercises = request.Exercises ?? [];
        var supersets = request.Supersets ?? [];

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
            {
                return Validation("name", "Enter a day name (1–80 characters).");
            }

            day.Name = name;
        }

        if (request.EstimatedDurationMinutes is { } dur &&
            (dur < 0 || dur > WorkoutLimits.MaxEstimatedDurationMinutes))
        {
            return Validation("estimatedDurationMinutes",
                $"Estimated duration must be between 0 and {WorkoutLimits.MaxEstimatedDurationMinutes} minutes.");
        }

        if (exercises.Count > WorkoutLimits.MaxExercisesPerDay)
        {
            return Validation("exercises", $"A day can hold at most {WorkoutLimits.MaxExercisesPerDay} exercises.");
        }

        if (supersets.Count > WorkoutLimits.MaxSupersetsPerDay)
        {
            return Validation("supersets", $"A day can hold at most {WorkoutLimits.MaxSupersetsPerDay} supersets.");
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
        db.SetPrescriptions.RemoveRange(day.Exercises.SelectMany(e => e.Sets));
        db.DayExercises.RemoveRange(day.Exercises);
        db.SupersetGroups.RemoveRange(day.Supersets);

        var refToId = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var s in supersets)
        {
            var groupEntity = new SupersetGroup
            {
                Id = Guid.NewGuid(),
                DayId = day.Id,
                SortOrder = s.SortOrder,
                RestAfterRoundSeconds = Math.Clamp(s.RestAfterRoundSeconds, 0, 3600),
            };
            db.SupersetGroups.Add(groupEntity);
            refToId[s.Ref] = groupEntity.Id;
        }

        foreach (var e in exercises)
        {
            var dayExercise = new DayExercise
            {
                Id = Guid.NewGuid(),
                DayId = day.Id,
                ExerciseId = e.ExerciseId,
                SortOrder = e.SortOrder,
                SupersetGroupId = e.SupersetRef is { } r ? refToId[r] : null,
                SupersetMemberOrder = e.SupersetMemberOrder,
                RestSeconds = e.RestSeconds,
                Notes = Trimmed(e.Notes),
            };

            foreach (var s in e.Sets ?? [])
            {
                dayExercise.Sets.Add(new SetPrescription
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

            db.DayExercises.Add(dayExercise);
        }

        day.EstimatedDurationMinutes = request.EstimatedDurationMinutes;

        // Null = leave the focus as-is; otherwise replace it, dropping any unknown muscle ids.
        if (request.FocusMuscleIds is not null)
        {
            var wanted = request.FocusMuscleIds.Distinct().ToList();
            day.FocusMuscleIds = wanted.Count == 0
                ? []
                : await db.Muscles.Where(m => wanted.Contains(m.Id)).Select(m => m.Id).ToListAsync(ct);
        }

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

        var day = await db.OwnedDays(userId).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (day is null)
        {
            return Results.NotFound();
        }

        db.WorkoutDays.Remove(day);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>
    /// Copy the chosen exercises from another of the caller's days into this one, as
    /// independent rows (new ids, copied sets — docs/08 Story 7 "copy creates independent
    /// ids"). A source superset is recreated here only if two or more of its members are in
    /// the selection; otherwise the copies land ungrouped. Appended after the current
    /// exercises.
    /// </summary>
    private static async Task<IResult> BulkCopyAsync(
        Guid id,
        BulkExerciseRequest request,
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var selectedIds = (request.DayExerciseIds ?? []).Distinct().ToList();
        if (selectedIds.Count == 0)
        {
            return Validation("dayExerciseIds", "Choose at least one exercise to copy.");
        }

        var destination = await db.OwnedDays(userId)
            .Include(d => d.Exercises)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (destination is null)
        {
            return Results.NotFound();
        }

        var source = await db.OwnedDays(userId)
            .Include(d => d.Exercises).ThenInclude(e => e.Sets)
            .Include(d => d.Supersets)
            .FirstOrDefaultAsync(d => d.Id == request.SourceDayId, ct);
        if (source is null)
        {
            return Results.NotFound();
        }

        var picked = source.Exercises.Where(e => selectedIds.Contains(e.Id)).OrderBy(e => e.SortOrder).ToList();
        if (picked.Count != selectedIds.Count)
        {
            return Validation("dayExerciseIds", "One or more exercises are not in the source day.");
        }

        if (destination.Exercises.Count + picked.Count > WorkoutLimits.MaxExercisesPerDay)
        {
            return TooMany($"A day can hold at most {WorkoutLimits.MaxExercisesPerDay} exercises.");
        }

        var program = await db.WorkoutPrograms.FirstAsync(p => p.Id == destination.ProgramId, ct);
        var nextSort = destination.Exercises.Count == 0 ? 0 : destination.Exercises.Max(e => e.SortOrder) + 1;

        // Recreate a superset here only when 2+ of its members were picked.
        var keptGroupIds = picked
            .Where(e => e.SupersetGroupId is not null)
            .GroupBy(e => e.SupersetGroupId!.Value)
            .Where(g => g.Count() >= 2)
            .Select(g => g.Key)
            .ToHashSet();

        var newGroupBySourceId = source.Supersets
            .Where(s => keptGroupIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => new SupersetGroup
            {
                Id = Guid.NewGuid(),
                DayId = destination.Id,
                SortOrder = s.SortOrder,
                RestAfterRoundSeconds = s.RestAfterRoundSeconds,
            });

        foreach (var group in newGroupBySourceId.Values)
        {
            db.SupersetGroups.Add(group);
        }

        foreach (var e in picked)
        {
            var copy = new DayExercise
            {
                Id = Guid.NewGuid(),
                DayId = destination.Id,
                ExerciseId = e.ExerciseId,
                SortOrder = nextSort++,
                SupersetGroup = e.SupersetGroupId is { } gid && newGroupBySourceId.TryGetValue(gid, out var ng) ? ng : null,
                SupersetMemberOrder = e.SupersetMemberOrder,
                RestSeconds = e.RestSeconds,
                Notes = e.Notes,
                Sets = e.Sets.OrderBy(s => s.SortOrder).Select(s => new SetPrescription
                {
                    Id = Guid.NewGuid(),
                    SortOrder = s.SortOrder,
                    Kind = s.Kind,
                    IsAmrap = s.IsAmrap,
                    TargetToFailure = s.TargetToFailure,
                    TargetRepsMin = s.TargetRepsMin,
                    TargetRepsMax = s.TargetRepsMax,
                    TargetWeightKg = s.TargetWeightKg,
                    TargetRir = s.TargetRir,
                }).ToList(),
            };
            db.DayExercises.Add(copy);
        }

        if (!await TrySaveWithRowVersionAsync(db, program, request.RowVersion, ct))
        {
            return StaleWrite();
        }

        return await GetAsync(id, http, db, ct);
    }

    /// <summary>
    /// Move the chosen exercises from another of the caller's days into this one, keeping
    /// their identity and sets (docs/04 "move preserves identity where the parent change
    /// allows"). Moved exercises leave their superset; a source superset left with fewer
    /// than two members is dissolved.
    /// </summary>
    private static async Task<IResult> BulkMoveAsync(
        Guid id,
        BulkExerciseRequest request,
        HttpContext http,
        MySelfDbContext db,
        CancellationToken ct)
    {
        if (!http.TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (request.SourceDayId == id)
        {
            return Validation("sourceDayId", "Source and destination day must differ.");
        }

        var selectedIds = (request.DayExerciseIds ?? []).Distinct().ToList();
        if (selectedIds.Count == 0)
        {
            return Validation("dayExerciseIds", "Choose at least one exercise to move.");
        }

        var destination = await db.OwnedDays(userId)
            .Include(d => d.Exercises)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (destination is null)
        {
            return Results.NotFound();
        }

        var source = await db.OwnedDays(userId)
            .Include(d => d.Exercises)
            .Include(d => d.Supersets)
            .FirstOrDefaultAsync(d => d.Id == request.SourceDayId, ct);
        if (source is null)
        {
            return Results.NotFound();
        }

        var picked = source.Exercises.Where(e => selectedIds.Contains(e.Id)).OrderBy(e => e.SortOrder).ToList();
        if (picked.Count != selectedIds.Count)
        {
            return Validation("dayExerciseIds", "One or more exercises are not in the source day.");
        }

        if (destination.Exercises.Count + picked.Count > WorkoutLimits.MaxExercisesPerDay)
        {
            return TooMany($"A day can hold at most {WorkoutLimits.MaxExercisesPerDay} exercises.");
        }

        var program = await db.WorkoutPrograms.FirstAsync(p => p.Id == destination.ProgramId, ct);
        var nextSort = destination.Exercises.Count == 0 ? 0 : destination.Exercises.Max(e => e.SortOrder) + 1;
        var touchedSourceGroupIds = picked.Where(e => e.SupersetGroupId is not null)
            .Select(e => e.SupersetGroupId!.Value).Distinct().ToList();

        foreach (var e in picked)
        {
            e.DayId = destination.Id;
            e.SortOrder = nextSort++;
            e.SupersetGroupId = null;
            e.SupersetMemberOrder = 0;
        }

        // A source superset that now has fewer than two members no longer makes sense.
        foreach (var groupId in touchedSourceGroupIds)
        {
            var remaining = source.Exercises.Count(e => e.SupersetGroupId == groupId && !selectedIds.Contains(e.Id));
            if (remaining < 2)
            {
                var group = source.Supersets.FirstOrDefault(s => s.Id == groupId);
                if (group is not null)
                {
                    db.SupersetGroups.Remove(group); // FK OnDelete(SetNull) clears any lone remaining member
                }
            }
        }

        if (!await TrySaveWithRowVersionAsync(db, program, request.RowVersion, ct))
        {
            return StaleWrite();
        }

        return await GetAsync(id, http, db, ct);
    }
}
