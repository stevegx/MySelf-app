using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Nutrition;
using MySelf.Infrastructure.Identity;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class MealCategoryConfiguration : IEntityTypeConfiguration<MealCategory>
{
    public void Configure(EntityTypeBuilder<MealCategory> builder)
    {
        builder.ToTable("meal_categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(40).IsRequired();

        // Every read is "this user's slots, in order". Two active slots may not share a name;
        // an archived row with the same name is fine (archive-then-recreate).
        builder.HasIndex(c => new { c.UserId, c.SortOrder });
        builder.HasIndex(c => new { c.UserId, c.Name })
            .IsUnique()
            .HasFilter("\"ArchivedAt\" IS NULL");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
