using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Auth;

/// <summary>Drives POST /api/v1/auth/logout. Cookies are handled manually, as in RefreshEndpointTests.</summary>
public class LogoutEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Password = "Str0ng!Passw0rd";

    private static string ExtractCookiePair(string setCookieHeader) => setCookieHeader.Split(';')[0];

    [Fact]
    public async Task Logout_revokes_the_refresh_token_and_clears_the_cookie()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        try
        {
            var registerResponse = await client.PostAsJsonAsync(
                "/api/v1/auth/register", new { username, email, password = Password });
            var cookie = ExtractCookiePair(Assert.Single(registerResponse.Headers.GetValues("Set-Cookie")));

            using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
            logoutRequest.Headers.Add("Cookie", cookie);
            var logoutResponse = await client.SendAsync(logoutRequest);

            Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
            var clearedCookie = Assert.Single(logoutResponse.Headers.GetValues("Set-Cookie"));
            Assert.Contains("1970", clearedCookie); // Cookies.Delete expires it in the past

            // The revoked cookie can no longer be exchanged for a new access token.
            using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
            refreshRequest.Headers.Add("Cookie", cookie);
            var refreshResponse = await client.SendAsync(refreshRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Logout_with_no_cookie_still_returns_204()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var response = await client.PostAsync("/api/v1/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_with_an_already_invalid_cookie_still_returns_204()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Cookie", "refreshToken=not-a-real-token");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
