using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Nutrition;
using MySelf.Infrastructure.Identity;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class SavedMealConfiguration : IEntityTypeConfiguration<SavedMeal>
{
    public void Configure(EntityTypeBuilder<SavedMeal> builder)
    {
        builder.ToTable("saved_meals");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).HasMaxLength(120).IsRequired();
        builder.Property(m => m.Category).HasMaxLength(40).IsRequired();
        builder.Property(m => m.Notes).HasMaxLength(500);

        builder.HasIndex(m => new { m.UserId, m.ArchivedAt, m.Name });

        builder.HasMany(m => m.Items)
            .WithOne(i => i.SavedMeal)
            .HasForeignKey(i => i.SavedMealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SavedMealItemConfiguration : IEntityTypeConfiguration<SavedMealItem>
{
    public void Configure(EntityTypeBuilder<SavedMealItem> builder)
    {
        builder.ToTable("saved_meal_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name).HasMaxLength(160).IsRequired();
        builder.Property(i => i.ServingBasis).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(i => i.Unit).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.Property(i => i.ServingSizeGrams).HasPrecision(8, 2);
        builder.Property(i => i.DefaultAmount).HasPrecision(9, 2);
        foreach (var col in new[]
                 {
                     nameof(SavedMealItem.BasisKcal), nameof(SavedMealItem.BasisProteinG),
                     nameof(SavedMealItem.BasisCarbG), nameof(SavedMealItem.BasisFatG),
                 })
        {
            builder.Property(col).HasPrecision(9, 2);
        }

        builder.HasIndex(i => new { i.SavedMealId, i.SortOrder });
    }
}
