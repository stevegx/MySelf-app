using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Auth;

/// <summary>
/// Drives POST /api/v1/auth/forgot-password and /reset-password. WebApplicationFactory
/// runs as Development by default, so forgot-password's response includes the real
/// resetLink (see PasswordResetEndpoints) — this test extracts the token straight from it
/// rather than needing to read server logs, and reset-password exercises the token for real.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PasswordResetEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    private const string OriginalPassword = "Str0ng!Passw0rd";
    private const string NewPassword = "EvenStr0nger!Pass";

    private static string ExtractCookiePair(string setCookieHeader) => setCookieHeader.Split(';')[0];

    private static (string Email, string Token) ExtractFromLink(string resetLink)
    {
        var query = QueryHelpers.ParseQuery(new Uri(resetLink).Query);
        return (query["email"].ToString(), query["token"].ToString());
    }

    [Fact]
    public async Task Forgot_password_for_a_known_email_returns_a_usable_reset_link_in_development()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();

        try
        {
            await client.PostAsJsonAsync("/api/v1/auth/register", new { username, email, password = OriginalPassword });

            var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var resetLink = body.GetProperty("resetLink").GetString();
            Assert.False(string.IsNullOrEmpty(resetLink));
            Assert.Contains("/reset-password?email=", resetLink);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_email_returns_the_same_generic_message_and_no_link()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password", new { email = "no-such-account@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            "If an account exists for that email, a reset link has been sent.",
            body.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("resetLink").ValueKind);
    }

    [Fact]
    public async Task Full_flow_reset_works_then_old_password_is_rejected_and_old_sessions_are_revoked()
    {
        var (username, email) = UniqueIdentity();
        // HandleCookies:false so this test controls exactly which cookie it sends, same
        // reasoning as RefreshEndpointTests/LogoutEndpointTests.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        try
        {
            var registerResponse = await client.PostAsJsonAsync(
                "/api/v1/auth/register", new { username, email, password = OriginalPassword });
            var oldSessionCookie = ExtractCookiePair(Assert.Single(registerResponse.Headers.GetValues("Set-Cookie")));

            var forgotResponse = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
            var forgotBody = await forgotResponse.Content.ReadFromJsonAsync<JsonElement>();
            var (linkEmail, token) = ExtractFromLink(forgotBody.GetProperty("resetLink").GetString()!);

            var resetResponse = await client.PostAsJsonAsync(
                "/api/v1/auth/reset-password", new { email = linkEmail, token, newPassword = NewPassword });
            Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

            // Old password no longer works.
            var loginWithOldPassword = await client.PostAsJsonAsync(
                "/api/v1/auth/login", new { identifier = email, password = OriginalPassword });
            Assert.Equal(HttpStatusCode.Unauthorized, loginWithOldPassword.StatusCode);

            // New password does.
            var loginWithNewPassword = await client.PostAsJsonAsync(
                "/api/v1/auth/login", new { identifier = email, password = NewPassword });
            Assert.Equal(HttpStatusCode.OK, loginWithNewPassword.StatusCode);

            // The session that existed *before* the reset is revoked — a device that stole
            // that cookie before the reset can no longer use it afterwards.
            using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
            refreshRequest.Headers.Add("Cookie", oldSessionCookie);
            var refreshResponse = await client.SendAsync(refreshRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Reset_with_an_invalid_token_returns_400()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();

        try
        {
            await client.PostAsJsonAsync("/api/v1/auth/register", new { username, email, password = OriginalPassword });

            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/reset-password",
                new { email, token = "not-a-real-token", newPassword = NewPassword });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Invalid reset link", body.GetProperty("title").GetString());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Reset_with_a_weak_new_password_returns_400_with_a_field_error()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();

        try
        {
            await client.PostAsJsonAsync("/api/v1/auth/register", new { username, email, password = OriginalPassword });
            var forgotResponse = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
            var forgotBody = await forgotResponse.Content.ReadFromJsonAsync<JsonElement>();
            var (linkEmail, token) = ExtractFromLink(forgotBody.GetProperty("resetLink").GetString()!);

            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/reset-password", new { email = linkEmail, token, newPassword = "123" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("errors").TryGetProperty("newPassword", out var errors));
            Assert.True(errors.GetArrayLength() > 0);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }
}
