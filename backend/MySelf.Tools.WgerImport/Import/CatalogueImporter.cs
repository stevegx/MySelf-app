using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Exercises;
using MySelf.Infrastructure.Persistence;
using MySelf.Tools.WgerImport.Snapshot;

namespace MySelf.Tools.WgerImport.Import;

public record ImportResult(int Categories, int Muscles, int Equipment, int Exercises, int Skipped);

/// <summary>
/// Applies a <see cref="CatalogueSnapshot"/> into the database. Idempotent: identified by
/// (Source, ExternalId), existing exercises are updated in place and their muscle/equipment
/// links rebuilt. Does not manage a transaction — the caller wraps it.
/// </summary>
public static class CatalogueImporter
{
    public static async Task<ImportResult> ImportAsync(
        MySelfDbContext db,
        CatalogueSnapshot snapshot,
        CancellationToken ct = default)
    {
        await UpsertLookupsAsync(db, snapshot, ct);
        await db.SaveChangesAsync(ct);

        var existing = await db.Exercises
            .Include(e => e.Muscles)
            .Include(e => e.Equipment)
            .Where(e => e.Source == snapshot.Source)
            .ToDictionaryAsync(e => e.ExternalId, ct);

        var validMuscleIds = snapshot.Muscles.Select(m => m.Id).ToHashSet();
        var validEquipmentIds = snapshot.Equipment.Select(e => e.Id).ToHashSet();
        var skipped = 0;

        foreach (var dto in snapshot.Exercises)
        {
            if (!existing.TryGetValue(dto.ExternalId, out var exercise))
            {
                exercise = new Exercise
                {
                    Id = Guid.NewGuid(),
                    Source = snapshot.Source,
                    ExternalId = dto.ExternalId,
                    Name = dto.Name,
                    SourceVersion = dto.SourceVersion,
                };
                db.Exercises.Add(exercise);
            }

            exercise.Name = dto.Name;
            exercise.CategoryId = dto.CategoryId;
            exercise.Instructions = dto.Instructions;
            exercise.DefaultTrackingMode = Enum.Parse<TrackingMode>(dto.TrackingMode);
            exercise.VariationGroupExternalId = dto.VariationGroupExternalId;
            exercise.SourceVersion = dto.SourceVersion;
            exercise.LicenseShortName = dto.LicenseShortName;
            exercise.LicenseUrl = dto.LicenseUrl;
            exercise.Attribution = dto.Attribution;
            exercise.ImageUrl = dto.ImageUrl;
            exercise.ImageThumbUrl = dto.ImageThumbUrl;
            exercise.ImageAttribution = dto.ImageAttribution;
            exercise.FetchedAt = snapshot.FetchedAt;

            exercise.Muscles.Clear();
            foreach (var id in dto.PrimaryMuscleIds.Where(validMuscleIds.Contains))
            {
                exercise.Muscles.Add(new ExerciseMuscle { MuscleId = id, Role = MuscleRole.Primary });
            }
            foreach (var id in dto.SecondaryMuscleIds.Where(validMuscleIds.Contains))
            {
                exercise.Muscles.Add(new ExerciseMuscle { MuscleId = id, Role = MuscleRole.Secondary });
            }

            exercise.Equipment.Clear();
            foreach (var id in dto.EquipmentIds.Where(validEquipmentIds.Contains))
            {
                exercise.Equipment.Add(new ExerciseEquipment { EquipmentId = id });
            }
        }

        await db.SaveChangesAsync(ct);

        return new ImportResult(
            snapshot.Categories.Count,
            snapshot.Muscles.Count,
            snapshot.Equipment.Count,
            snapshot.Exercises.Count,
            skipped);
    }

    private static async Task UpsertLookupsAsync(
        MySelfDbContext db,
        CatalogueSnapshot snapshot,
        CancellationToken ct)
    {
        var categories = await db.ExerciseCategories.ToDictionaryAsync(c => c.Id, ct);
        foreach (var dto in snapshot.Categories)
        {
            if (categories.TryGetValue(dto.Id, out var existing))
            {
                existing.Name = dto.Name;
            }
            else
            {
                db.ExerciseCategories.Add(new ExerciseCategory { Id = dto.Id, Name = dto.Name });
            }
        }

        var muscles = await db.Muscles.ToDictionaryAsync(m => m.Id, ct);
        foreach (var dto in snapshot.Muscles)
        {
            if (muscles.TryGetValue(dto.Id, out var existing))
            {
                existing.Name = dto.Name;
                existing.LatinName = dto.LatinName;
                existing.IsFront = dto.IsFront;
            }
            else
            {
                db.Muscles.Add(new Muscle
                {
                    Id = dto.Id,
                    Name = dto.Name,
                    LatinName = dto.LatinName,
                    IsFront = dto.IsFront,
                });
            }
        }

        var equipment = await db.Equipment.ToDictionaryAsync(e => e.Id, ct);
        foreach (var dto in snapshot.Equipment)
        {
            if (equipment.TryGetValue(dto.Id, out var existing))
            {
                existing.Name = dto.Name;
            }
            else
            {
                db.Equipment.Add(new Equipment { Id = dto.Id, Name = dto.Name });
            }
        }
    }
}
