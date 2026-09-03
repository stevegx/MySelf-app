using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Auth;

/// <summary>
/// Drives POST /api/v1/auth/refresh. Cookies are handled manually (extracted from
/// Set-Cookie, attached via the Cookie header on the next request) rather than relying on
/// HttpClient's own cookie jar, so each step is explicit about which cookie value it sends.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class RefreshEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    private const string Password = "Str0ng!Passw0rd";

    private sealed record Registered(string AccessToken, string RefreshCookie);

    /// <summary>Pulls just the "name=value" pair out of a raw Set-Cookie header string.</summary>
    private static string ExtractCookiePair(string setCookieHeader) => setCookieHeader.Split(';')[0];

    private static async Task<Registered> RegisterAsync(
        HttpClient client, string username, string email, bool trustThisDevice)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { username, email, password = Password, trustThisDevice });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var cookie = ExtractCookiePair(Assert.Single(response.Headers.GetValues("Set-Cookie")));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Registered(body.GetProperty("accessToken").GetString()!, cookie);
    }

    [Fact]
    public async Task Valid_cookie_returns_a_new_access_token_and_rotates_the_cookie()
    {
        var (username, email) = UniqueIdentity();
        // HandleCookies:false — otherwise the client's own cookie jar would auto-attach
        // whatever it last saw, silently interfering with the manual Cookie headers below.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        try
        {
            var registered = await RegisterAsync(client, username, email, trustThisDevice: true);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
            request.Headers.Add("Cookie", registered.RefreshCookie);
            var refreshResponse = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
            var body = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
            var newAccessToken = body.GetProperty("accessToken").GetString();
            Assert.False(string.IsNullOrEmpty(newAccessToken));
            Assert.NotEqual(registered.AccessToken, newAccessToken);

            var newCookie = Assert.Single(refreshResponse.Headers.GetValues("Set-Cookie"));
            Assert.Contains("expires=", newCookie, StringComparison.OrdinalIgnoreCase); // rotation kept "trusted"
            Assert.NotEqual(registered.RefreshCookie, ExtractCookiePair(newCookie));
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Rotated_cookie_cannot_be_reused()
    {
        var (username, email) = UniqueIdentity();
        // HandleCookies:false — otherwise the client's own cookie jar would auto-attach
        // whatever it last saw, silently interfering with the manual Cookie headers below.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        try
        {
            var registered = await RegisterAsync(client, username, email, trustThisDevice: true);

            using var first = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
            first.Headers.Add("Cookie", registered.RefreshCookie);
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(first)).StatusCode);

            // Same (now-revoked) cookie again — must fail, proving rotation actually revokes.
            using var replay = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
            replay.Headers.Add("Cookie", registered.RefreshCookie);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(replay)).StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task No_cookie_returns_401()
    {
        // HandleCookies:false — otherwise the client's own cookie jar would auto-attach
        // whatever it last saw, silently interfering with the manual Cookie headers below.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var response = await client.PostAsync("/api/v1/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Garbage_cookie_returns_401()
    {
        // HandleCookies:false — otherwise the client's own cookie jar would auto-attach
        // whatever it last saw, silently interfering with the manual Cookie headers below.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", "refreshToken=not-a-real-token");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
