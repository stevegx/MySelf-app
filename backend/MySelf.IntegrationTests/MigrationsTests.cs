using Microsoft.EntityFrameworkCore;
using MySelf.Infrastructure.Persistence;

namespace MySelf.IntegrationTests;

/// <summary>
/// Guards that the EF Core model and the committed migrations stay in sync.
/// Goes red the moment an entity is added or changed without a matching
/// `dotnet ef migrations add`. No database connection is opened.
/// </summary>
public class MigrationsTests
{
    [Fact]
    public void Model_has_no_changes_that_are_missing_from_a_migration()
    {
        // Arrange — a provider is required so EF can build the model; no connection is opened.
        var options = new DbContextOptionsBuilder<MySelfDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        using var context = new MySelfDbContext(options);

        // Act
        var hasPendingChanges = context.Database.HasPendingModelChanges();

        // Assert
        Assert.False(
            hasPendingChanges,
            "The EF Core model has changes with no corresponding migration. Run: "
            + "dotnet ef migrations add <Name> --project MySelf.Infrastructure --startup-project MySelf.Api");
    }
}
