using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Exercises;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class ExerciseCategoryConfiguration : IEntityTypeConfiguration<ExerciseCategory>
{
    public void Configure(EntityTypeBuilder<ExerciseCategory> builder)
    {
        builder.ToTable("exercise_categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever(); // == wger id
        builder.Property(c => c.Name).HasMaxLength(60).IsRequired();
    }
}
