using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySelf.Infrastructure.Persistence;

namespace MySelf.IntegrationTests.Auth;

/// <summary>Shared by Register/Login/Me endpoint tests, which all create real Identity users.</summary>
internal static class AuthTestHelpers
{
    public static (string Username, string Email) UniqueIdentity()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        return ($"authtest{suffix}", $"auth-test-{suffix}@example.com");
    }

    public static async Task DeleteUsersAsync(this WebApplicationFactory<Program> factory, params string[] emails)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MySelfDbContext>();

        foreach (var email in emails)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null)
            {
                continue;
            }

            await db.RefreshTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync();
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
        }
    }
}
