using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

    /// <summary>
    /// Registers a fresh unique user and returns a client with its bearer token already set,
    /// plus the email so the caller can clean up with <see cref="DeleteUsersAsync"/>.
    /// </summary>
    public static async Task<(HttpClient Client, string Email)> RegisterAndAuthenticateAsync(
        this WebApplicationFactory<Program> factory)
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { username, email, password = "Str0ng!Passw0rd" });
        register.EnsureSuccessStatusCode();

        var body = await register.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return (client, email);
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
