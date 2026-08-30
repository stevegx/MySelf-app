using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Exercises;
using MySelf.Domain.Nutrition;

namespace MySelf.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit-of-work + change tracker for the MySelf database.
/// Entity mappings live in <c>Persistence/Configurations</c> and are picked up by
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>.
/// </summary>
public class MySelfDbContext(DbContextOptions<MySelfDbContext> options) : DbContext(options)
{
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ExerciseCategory> ExerciseCategories => Set<ExerciseCategory>();
    public DbSet<Muscle> Muscles => Set<Muscle>();
    public DbSet<Equipment> Equipment => Set<Equipment>();

    public DbSet<FoodCacheEntry> FoodCacheEntries => Set<FoodCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MySelfDbContext).Assembly);
    }
}
