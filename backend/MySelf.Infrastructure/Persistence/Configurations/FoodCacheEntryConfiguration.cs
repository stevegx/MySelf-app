using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Nutrition;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class FoodCacheEntryConfiguration : IEntityTypeConfiguration<FoodCacheEntry>
{
    public void Configure(EntityTypeBuilder<FoodCacheEntry> builder)
    {
        builder.ToTable("food_cache_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Source).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Barcode).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => new { e.Source, e.Barcode }).IsUnique();

        builder.Property(e => e.Name).HasMaxLength(300);
        builder.Property(e => e.Brand).HasMaxLength(300);
        builder.Property(e => e.SourceUrl).HasMaxLength(400);
        builder.Property(e => e.ServingSizeRaw).HasMaxLength(80);
        builder.Property(e => e.PackageQuantityRaw).HasMaxLength(80);

        foreach (var nutrient in new[]
                 {
                     nameof(FoodCacheEntry.EnergyKcalPer100g),
                     nameof(FoodCacheEntry.ProteinPer100g),
                     nameof(FoodCacheEntry.CarbsPer100g),
                     nameof(FoodCacheEntry.FatPer100g),
                     nameof(FoodCacheEntry.SaturatedFatPer100g),
                     nameof(FoodCacheEntry.SugarsPer100g),
                     nameof(FoodCacheEntry.FiberPer100g),
                     nameof(FoodCacheEntry.SaltPer100g),
                     nameof(FoodCacheEntry.SodiumPer100g),
                     nameof(FoodCacheEntry.ServingQuantityGrams),
                 })
        {
            builder.Property(nutrient).HasPrecision(10, 3);
        }

        builder.Property(e => e.RawPayload).HasColumnType("jsonb");
    }
}
