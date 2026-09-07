using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Nutrition;
using MySelf.Infrastructure.Identity;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class MealLogConfiguration : IEntityTypeConfiguration<MealLog>
{
    public void Configure(EntityTypeBuilder<MealLog> builder)
    {
        builder.ToTable("meal_logs");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Category).HasMaxLength(40).IsRequired();

        // Every read is "this user's meals on a date"; and the lazy-create path looks up the
        // exact (user, date, category) row. One meal per slot.
        builder.HasIndex(m => new { m.UserId, m.LocalDate, m.Category }).IsUnique();

        builder.HasMany(m => m.Items)
            .WithOne(i => i.MealLog)
            .HasForeignKey(i => i.MealLogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
