using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Nutrition;
using MySelf.Infrastructure.Identity;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class CustomFoodConfiguration : IEntityTypeConfiguration<CustomFood>
{
    public void Configure(EntityTypeBuilder<CustomFood> builder)
    {
        builder.ToTable("custom_foods");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).HasMaxLength(160).IsRequired();
        builder.Property(f => f.Brand).HasMaxLength(120);
        builder.Property(f => f.Barcode).HasMaxLength(14);
        builder.Property(f => f.ServingBasis).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.Property(f => f.ServingSizeGrams).HasPrecision(8, 2);
        foreach (var col in new[]
                 {
                     nameof(CustomFood.Kcal), nameof(CustomFood.ProteinG),
                     nameof(CustomFood.CarbG), nameof(CustomFood.FatG),
                 })
        {
            builder.Property(col).HasPrecision(9, 2);
        }

        // The picker reads "this user's live foods, best name matches first".
        builder.HasIndex(f => new { f.UserId, f.ArchivedAt, f.Name });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
