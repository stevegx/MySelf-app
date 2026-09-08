using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Nutrition;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class MealLogItemConfiguration : IEntityTypeConfiguration<MealLogItem>
{
    public void Configure(EntityTypeBuilder<MealLogItem> builder)
    {
        builder.ToTable("meal_log_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name).HasMaxLength(160).IsRequired();
        builder.Property(i => i.ServingBasis).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(i => i.Unit).HasConversion<string>().HasMaxLength(16).IsRequired();

        // Nutrients and amounts are money-like — decimal, never binary float (docs/04 conventions).
        builder.Property(i => i.ServingSizeGrams).HasPrecision(8, 2);
        builder.Property(i => i.Amount).HasPrecision(9, 2);
        foreach (var col in new[]
                 {
                     nameof(MealLogItem.BasisKcal), nameof(MealLogItem.BasisProteinG),
                     nameof(MealLogItem.BasisCarbG), nameof(MealLogItem.BasisFatG),
                     nameof(MealLogItem.Kcal), nameof(MealLogItem.ProteinG),
                     nameof(MealLogItem.CarbG), nameof(MealLogItem.FatG),
                 })
        {
            builder.Property(col).HasPrecision(9, 2);
        }

        builder.HasIndex(i => new { i.MealLogId, i.SortOrder });
    }
}
