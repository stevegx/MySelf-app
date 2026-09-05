using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Exercises;
using MySelf.Domain.Workouts;
using MySelf.Infrastructure.Identity;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class WorkoutProgramConfiguration : IEntityTypeConfiguration<WorkoutProgram>
{
    public void Configure(EntityTypeBuilder<WorkoutProgram> builder)
    {
        builder.ToTable("workout_programs");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(120).IsRequired();
        builder.Property(p => p.SplitLabel).HasMaxLength(60);

        // Maps to PostgreSQL's hidden xmin system column — a concurrency token with no
        // extra table column. SaveChanges throws DbUpdateConcurrencyException if the row
        // changed since it was read.
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasIndex(p => p.UserId);

        // Only one active program per user — a filtered unique index enforces it in the DB,
        // not just in the activate handler.
        builder.HasIndex(p => p.UserId)
            .HasFilter("\"IsActive\" = true")
            .IsUnique()
            .HasDatabaseName("IX_workout_programs_UserId_active");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Days)
            .WithOne(d => d.Program)
            .HasForeignKey(d => d.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkoutDayConfiguration : IEntityTypeConfiguration<WorkoutDay>
{
    public void Configure(EntityTypeBuilder<WorkoutDay> builder)
    {
        builder.ToTable("workout_days");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(d => new { d.ProgramId, d.SortOrder });

        builder.HasMany(d => d.Exercises)
            .WithOne(e => e.Day)
            .HasForeignKey(e => e.DayId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Supersets)
            .WithOne(s => s.Day)
            .HasForeignKey(s => s.DayId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SupersetGroupConfiguration : IEntityTypeConfiguration<SupersetGroup>
{
    public void Configure(EntityTypeBuilder<SupersetGroup> builder)
    {
        builder.ToTable("superset_groups");
        builder.HasKey(s => s.Id);
    }
}

public class DayExerciseConfiguration : IEntityTypeConfiguration<DayExercise>
{
    public void Configure(EntityTypeBuilder<DayExercise> builder)
    {
        builder.ToTable("day_exercises");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.HasIndex(e => new { e.DayId, e.SortOrder });

        // Restrict, not Cascade: a catalogue exercise must never be deletable out from under
        // a program that references it.
        builder.HasOne(e => e.Exercise)
            .WithMany()
            .HasForeignKey(e => e.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        // The superset link is optional; clearing the group (ungroup) just nulls this.
        builder.HasOne(e => e.SupersetGroup)
            .WithMany()
            .HasForeignKey(e => e.SupersetGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.Sets)
            .WithOne(s => s.DayExercise)
            .HasForeignKey(s => s.DayExerciseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SetPrescriptionConfiguration : IEntityTypeConfiguration<SetPrescription>
{
    public void Configure(EntityTypeBuilder<SetPrescription> builder)
    {
        builder.ToTable("set_prescriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.TargetWeightKg).HasPrecision(6, 2);
        builder.HasIndex(s => new { s.DayExerciseId, s.SortOrder });
    }
}
