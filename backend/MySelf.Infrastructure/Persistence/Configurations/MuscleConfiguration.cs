using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Exercises;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class MuscleConfiguration : IEntityTypeConfiguration<Muscle>
{
    public void Configure(EntityTypeBuilder<Muscle> builder)
    {
        builder.ToTable("muscles");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever(); // == wger id
        builder.Property(m => m.Name).HasMaxLength(80).IsRequired();
        builder.Property(m => m.LatinName).HasMaxLength(80).IsRequired();
    }
}
