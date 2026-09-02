using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Identity;
using MySelf.Infrastructure.Identity;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");

        // UserId is the whole key — no separate surrogate id. ValueGeneratedNever() tells EF
        // the app supplies it (it is the caller's user id), so it must not ask the database
        // to generate one.
        builder.HasKey(p => p.UserId);
        builder.Property(p => p.UserId).ValueGeneratedNever();

        builder.Property(p => p.DateOfBirth).IsRequired();

        // 5,2 => up to 999.99 cm; the endpoint clamps to a human range well inside that.
        builder.Property(p => p.HeightCm).HasPrecision(5, 2).IsRequired();

        // Enums persist as their names, matching the ExerciseConfiguration convention — a
        // readable "Male"/"Metric" in the column instead of an int whose meaning drifts if
        // the enum is ever reordered.
        builder.Property(p => p.CalculationSex)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(p => p.UnitSystem)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.Timezone).HasMaxLength(64);
        builder.Property(p => p.Locale).HasMaxLength(16);

        // One-to-one with the Identity user, no navigation property on either side. Deleting
        // the account deletes its profile.
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
