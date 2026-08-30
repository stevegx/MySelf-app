using Microsoft.EntityFrameworkCore;

namespace MySelf.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit-of-work + change tracker for the MySelf database.
/// Empty for now: it holds no <see cref="DbSet{TEntity}"/> yet. Entities and the
/// first migration are added in a later slice.
/// </summary>
public class MySelfDbContext(DbContextOptions<MySelfDbContext> options) : DbContext(options)
{
}
