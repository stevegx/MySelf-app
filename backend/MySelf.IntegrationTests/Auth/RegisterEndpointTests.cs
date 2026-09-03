using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySelf.Infrastructure.Persistence;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Auth;

/// <summary>
/// Drives POST /api/v1/auth/register through the real pipeline (Identity user creation +
/// JWT/refresh-token issuance), against the shared dev database. Each test uses a unique
/// username/email pair and deletes what it created afterwards, the same pattern as
/// BarcodeEndpointTests.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RegisterEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Valid_registration_returns_201_with_access_token_and_refresh_cookie()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();

        try
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/register",
                new { username, email, password = "Str0ng!Passw0rd" });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
            Assert.Equal(username, body.GetProperty("user").GetProperty("username").GetString());
            Assert.Equal(email, body.GetProperty("user").GetProperty("email").GetString());

            var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
            Assert.Contains("refreshToken=", setCookie);
            Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MySelfDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            Assert.Equal(username, user.UserName);
            Assert.NotEqual("Str0ng!Passw0rd", user.PasswordHash);
            Assert.True(await db.RefreshTokens.AnyAsync(t => t.UserId == user.Id));
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Duplicate_email_returns_400_with_email_field_error()
    {
        var (username1, email) = UniqueIdentity();
        var (username2, _) = UniqueIdentity();
        var client = factory.CreateClient();

        try
        {
            var first = await client.PostAsJsonAsync(
                "/api/v1/auth/register",
                new { username = username1, email, password = "Str0ng!Passw0rd" });
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            var second = await client.PostAsJsonAsync(
                "/api/v1/auth/register",
                new { username = username2, email, password = "AnotherStr0ng!Pass" });

            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
            var body = await second.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("errors").TryGetProperty("email", out var emailErrors));
            Assert.True(emailErrors.GetArrayLength() > 0);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Duplicate_username_returns_400_with_username_field_error()
    {
        var (username, email1) = UniqueIdentity();
        var (_, email2) = UniqueIdentity();
        var client = factory.CreateClient();

        try
        {
            var first = await client.PostAsJsonAsync(
                "/api/v1/auth/register",
                new { username, email = email1, password = "Str0ng!Passw0rd" });
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            var second = await client.PostAsJsonAsync(
                "/api/v1/auth/register",
                new { username, email = email2, password = "AnotherStr0ng!Pass" });

            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
            var body = await second.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("errors").TryGetProperty("username", out var usernameErrors));
            Assert.True(usernameErrors.GetArrayLength() > 0);
        }
        finally
        {
            await factory.DeleteUsersAsync(email1);
        }
    }

    [Fact]
    public async Task Weak_password_returns_400_with_field_errors()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { username, email, password = "123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("password", out var passwordErrors));
        Assert.True(passwordErrors.GetArrayLength() > 0);
    }
}
