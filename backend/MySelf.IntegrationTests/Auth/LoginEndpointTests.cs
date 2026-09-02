using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Auth;

/// <summary>
/// Drives POST /api/v1/auth/login through the real pipeline. Each test registers its own
/// account first (via the real /register endpoint) and deletes it afterwards.
/// </summary>
public class LoginEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Password = "Str0ng!Passw0rd";

    private static async Task RegisterAsync(HttpClient client, string username, string email) =>
        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync(
                "/api/v1/auth/register",
                new { username, email, password = Password })).StatusCode);

    [Fact]
    public async Task Correct_credentials_by_email_return_200_with_access_token_and_refresh_cookie()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();

        try
        {
            await RegisterAsync(client, username, email);

            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { identifier = email, password = Password });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
            Assert.Equal(username, body.GetProperty("user").GetProperty("username").GetString());

            var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
            Assert.Contains("refreshToken=", setCookie);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Correct_credentials_by_username_also_return_200()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();

        try
        {
            await RegisterAsync(client, username, email);

            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { identifier = username, password = Password });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(email, body.GetProperty("user").GetProperty("email").GetString());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Trust_this_device_sets_a_persistent_cookie_otherwise_a_session_cookie()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();

        try
        {
            await RegisterAsync(client, username, email);

            var trusted = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { identifier = email, password = Password, trustThisDevice = true });
            var trustedCookie = Assert.Single(trusted.Headers.GetValues("Set-Cookie"));
            Assert.Contains("expires=", trustedCookie, StringComparison.OrdinalIgnoreCase);

            var untrusted = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { identifier = email, password = Password, trustThisDevice = false });
            var untrustedCookie = Assert.Single(untrusted.Headers.GetValues("Set-Cookie"));
            Assert.DoesNotContain("expires=", untrustedCookie, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Wrong_password_returns_401_generic_message()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();

        try
        {
            await RegisterAsync(client, username, email);

            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { identifier = email, password = "TotallyWrong!1" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Invalid email or password.", body.GetProperty("detail").GetString());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Unknown_identifier_returns_the_same_401_generic_message()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier = "no-such-account@example.com", password = "Whatever!1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid email or password.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Account_locks_after_repeated_failed_attempts()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();

        try
        {
            await RegisterAsync(client, username, email);

            // Program.cs sets MaxFailedAccessAttempts = 5.
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var failed = await client.PostAsJsonAsync(
                    "/api/v1/auth/login",
                    new { identifier = email, password = "TotallyWrong!1" });
                Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
            }

            // Locked out now — even the *correct* password is rejected until it expires.
            var lockedOut = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { identifier = email, password = Password });

            Assert.Equal(HttpStatusCode.Locked, lockedOut.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }
}
