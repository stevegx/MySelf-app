using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Exercises;
using MySelf.Domain.Identity;
using MySelf.Domain.Nutrition;
using MySelf.Domain.Workouts;
using MySelf.Infrastructure.Identity;

namespace MySelf.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit-of-work + change tracker for the MySelf database.
/// Entity mappings live in <c>Persistence/Configurations</c> and are picked up by
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>.
///
/// Base class is <see cref="IdentityUserContext{TUser,TKey}"/> rather than plain
/// <see cref="DbContext"/> — it adds the Identity user/claims/logins/tokens tables and their
/// model configuration via base.OnModelCreating. This is the "no roles" Identity context
/// (as opposed to IdentityDbContext, which also adds Roles/RoleClaims); nothing here needs
/// role-based authorization yet, only per-owner checks, so the extra tables were left out.
/// </summary>
public class MySelfDbContext(DbContextOptions<MySelfDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ExerciseCategory> ExerciseCategories => Set<ExerciseCategory>();
    public DbSet<Muscle> Muscles => Set<Muscle>();
    public DbSet<Equipment> Equipment => Set<Equipment>();

    public DbSet<FoodCacheEntry> FoodCacheEntries => Set<FoodCacheEntry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserGoal> UserGoals => Set<UserGoal>();
    public DbSet<NutritionEstimateSnapshot> NutritionEstimateSnapshots => Set<NutritionEstimateSnapshot>();

    public DbSet<WorkoutProgram> WorkoutPrograms => Set<WorkoutProgram>();
    public DbSet<WorkoutGroup> WorkoutGroups => Set<WorkoutGroup>();
    public DbSet<WorkoutVariant> WorkoutVariants => Set<WorkoutVariant>();
    public DbSet<SupersetGroup> SupersetGroups => Set<SupersetGroup>();
    public DbSet<VariantExercise> VariantExercises => Set<VariantExercise>();
    public DbSet<SetPrescription> SetPrescriptions => Set<SetPrescription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MySelfDbContext).Assembly);
    }
}
