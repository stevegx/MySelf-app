using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySelf.Domain.Identity;
using MySelf.Infrastructure.Identity;

namespace MySelf.Infrastructure.Persistence.Configurations;

public class UserGoalConfiguration : IEntityTypeConfiguration<UserGoal>
{
    public void Configure(EntityTypeBuilder<UserGoal> builder)
    {
        builder.ToTable("user_goals");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.GoalType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(g => g.Source).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(g => g.TargetWeightKg).HasPrecision(6, 2);

        // Every read is "this user's goals, newest first" (GET /me/goals) or "this user's
        // latest goal" (GET /me) — a composite index on (UserId, EffectiveFrom) covers both.
        builder.HasIndex(g => new { g.UserId, g.EffectiveFrom });

        // Real FK to the account, cascade so deleting a user removes their goal history.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(g => g.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
