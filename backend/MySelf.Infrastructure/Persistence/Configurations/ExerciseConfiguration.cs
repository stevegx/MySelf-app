using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Exercises;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("exercises");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Instructions);

        builder.Property(e => e.DefaultTrackingMode)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(e => e.Source).HasMaxLength(40).IsRequired();
        builder.Property(e => e.ExternalId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.VariationGroupExternalId).HasMaxLength(64);
        builder.Property(e => e.SourceVersion).HasMaxLength(64).IsRequired();
        builder.Property(e => e.LicenseShortName).HasMaxLength(40);
        builder.Property(e => e.LicenseUrl).HasMaxLength(255);
        // Attribution is display metadata and can list many contributors — left as unbounded text.

        builder.Property(e => e.ImageUrl).HasMaxLength(500);
        builder.Property(e => e.ImageThumbUrl).HasMaxLength(500);
        builder.Property(e => e.ImageAttribution).HasMaxLength(255);

        builder.HasIndex(e => new { e.Source, e.ExternalId }).IsUnique();
        builder.HasIndex(e => e.Name);

        builder.HasOne(e => e.Category)
            .WithMany(c => c.Exercises)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Muscles)
            .WithOne(em => em.Exercise)
            .HasForeignKey(em => em.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Equipment)
            .WithOne(ee => ee.Exercise)
            .HasForeignKey(ee => ee.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExerciseMuscleConfiguration : IEntityTypeConfiguration<ExerciseMuscle>
{
    public void Configure(EntityTypeBuilder<ExerciseMuscle> builder)
    {
        builder.ToTable("exercise_muscles");
        builder.HasKey(em => new { em.ExerciseId, em.MuscleId });
        builder.Property(em => em.Role).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(em => em.Muscle)
            .WithMany(m => m.ExerciseMuscles)
            .HasForeignKey(em => em.MuscleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ExerciseEquipmentConfiguration : IEntityTypeConfiguration<ExerciseEquipment>
{
    public void Configure(EntityTypeBuilder<ExerciseEquipment> builder)
    {
        builder.ToTable("exercise_equipment");
        builder.HasKey(ee => new { ee.ExerciseId, ee.EquipmentId });

        builder.HasOne(ee => ee.Equipment)
            .WithMany(eq => eq.ExerciseEquipment)
            .HasForeignKey(ee => ee.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
