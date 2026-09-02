using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Identity;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class NutritionEstimateSnapshotConfiguration : IEntityTypeConfiguration<NutritionEstimateSnapshot>
{
    public void Configure(EntityTypeBuilder<NutritionEstimateSnapshot> builder)
    {
        builder.ToTable("nutrition_estimate_snapshots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.WeightKg).HasPrecision(6, 2);
        builder.Property(s => s.HeightCm).HasPrecision(5, 2);
        builder.Property(s => s.CalculationSex).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(s => s.ActivityLevel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.FormulaName).HasMaxLength(40).IsRequired();
        builder.Property(s => s.FormulaVersion).HasMaxLength(20).IsRequired();

        // One snapshot per goal; cascade so it goes when the goal (or the account) does.
        builder.HasOne<UserGoal>()
            .WithMany()
            .HasForeignKey(s => s.UserGoalId)
            .OnDelete(DeleteBehavior.Cascade);

        // UserId is kept for "all snapshots for this user" queries but left as a plain
        // indexed column (no FK) — a real FK here would add a second cascade path to
        // AspNetUsers alongside the one through user_goals. Same choice as RefreshToken.
        builder.HasIndex(s => s.UserId);
    }
}
