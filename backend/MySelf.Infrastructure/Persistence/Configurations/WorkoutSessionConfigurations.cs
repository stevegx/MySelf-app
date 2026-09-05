using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Workouts;
using MySelf.Infrastructure.Identity;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class WorkoutSessionConfiguration : IEntityTypeConfiguration<WorkoutSession>
{
    public void Configure(EntityTypeBuilder<WorkoutSession> builder)
    {
        builder.ToTable("workout_sessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.DayName).HasMaxLength(80);
        builder.Property(s => s.ProgramName).HasMaxLength(120);
        builder.Property(s => s.Notes).HasMaxLength(2000);

        builder.HasIndex(s => s.UserId);

        // Only one InProgress session per user — a filtered unique index enforces it in the
        // DB, not just in the start handler (same pattern as the one-active-program index).
        builder.HasIndex(s => s.UserId)
            .HasFilter("\"Status\" = 'InProgress'")
            .IsUnique()
            .HasDatabaseName("IX_workout_sessions_UserId_in_progress");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.ExerciseLogs)
            .WithOne(e => e.Session)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExerciseLogConfiguration : IEntityTypeConfiguration<ExerciseLog>
{
    public void Configure(EntityTypeBuilder<ExerciseLog> builder)
    {
        builder.ToTable("exercise_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ExerciseName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.TrackingMode).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.HasIndex(e => new { e.SessionId, e.SortOrder });

        // Restrict, not Cascade: a catalogue exercise must never be deletable out from under
        // a session that references it (same rule as VariantExercise).
        builder.HasOne<MySelf.Domain.Exercises.Exercise>()
            .WithMany()
            .HasForeignKey(e => e.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Sets)
            .WithOne(s => s.ExerciseLog)
            .HasForeignKey(s => s.ExerciseLogId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SetLogConfiguration : IEntityTypeConfiguration<SetLog>
{
    public void Configure(EntityTypeBuilder<SetLog> builder)
    {
        builder.ToTable("set_logs");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.TargetWeightKg).HasPrecision(6, 2);
        builder.Property(s => s.WeightKg).HasPrecision(6, 2);
        builder.Property(s => s.AddedWeightKg).HasPrecision(6, 2);
        builder.Property(s => s.AssistanceKg).HasPrecision(6, 2);
        builder.Property(s => s.DistanceMeters).HasPrecision(8, 2);
        builder.Property(s => s.SkippedReason).HasMaxLength(200);
        builder.HasIndex(s => new { s.ExerciseLogId, s.SortOrder });
    }
}
