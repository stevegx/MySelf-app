using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Identity;
using MySelf.Infrastructure.Identity;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class BodyMeasurementConfiguration : IEntityTypeConfiguration<BodyMeasurement>
{
    public void Configure(EntityTypeBuilder<BodyMeasurement> builder)
    {
        builder.ToTable("body_measurements");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.WeightKg).HasPrecision(6, 2).IsRequired();

        // Every read is "this user's readings over a date window, newest first".
        builder.HasIndex(m => new { m.UserId, m.LocalDate });

        // Real FK to the account, cascade so deleting a user removes their weight history.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
