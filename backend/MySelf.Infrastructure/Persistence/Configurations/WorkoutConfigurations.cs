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

        builder.HasMany(p => p.Groups)
            .WithOne(g => g.Program)
            .HasForeignKey(g => g.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkoutGroupConfiguration : IEntityTypeConfiguration<WorkoutGroup>
{
    public void Configure(EntityTypeBuilder<WorkoutGroup> builder)
    {
        builder.ToTable("workout_groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(g => new { g.ProgramId, g.SortOrder });

        builder.HasMany(g => g.Variants)
            .WithOne(v => v.Group)
            .HasForeignKey(v => v.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkoutVariantConfiguration : IEntityTypeConfiguration<WorkoutVariant>
{
    public void Configure(EntityTypeBuilder<WorkoutVariant> builder)
    {
        builder.ToTable("workout_variants");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(v => new { v.GroupId, v.SortOrder });

        builder.HasMany(v => v.Exercises)
            .WithOne(e => e.Variant)
            .HasForeignKey(e => e.VariantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Supersets)
            .WithOne(s => s.Variant)
            .HasForeignKey(s => s.VariantId)
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

public class VariantExerciseConfiguration : IEntityTypeConfiguration<VariantExercise>
{
    public void Configure(EntityTypeBuilder<VariantExercise> builder)
    {
        builder.ToTable("variant_exercises");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.HasIndex(e => new { e.VariantId, e.SortOrder });

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
            .WithOne(s => s.VariantExercise)
            .HasForeignKey(s => s.VariantExerciseId)
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
        builder.HasIndex(s => new { s.VariantExerciseId, s.SortOrder });
    }
}
