using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Me;

/// <summary>
/// Drives GET /api/v1/me — the first endpoint behind the JWT-bearer authentication
/// middleware wired up in Program.cs. Proves the middleware both accepts a valid token
/// issued by /register and rejects missing/invalid ones.
/// </summary>
public class MeEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Valid_access_token_returns_200_with_the_signed_in_user()
    {
        var (username, email) = UniqueIdentity();
        var client = factory.CreateClient();

        try
        {
            var registerResponse = await client.PostAsJsonAsync(
                "/api/v1/auth/register",
                new { username, email, password = "Str0ng!Passw0rd" });
            var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
            var accessToken = registerBody.GetProperty("accessToken").GetString();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetAsync("/api/v1/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var user = body.GetProperty("user");
            Assert.Equal(username, user.GetProperty("username").GetString());
            Assert.Equal(email, user.GetProperty("email").GetString());

            // A brand-new account has not started onboarding, so there is no profile row yet.
            Assert.Equal(JsonValueKind.Null, body.GetProperty("profile").ValueKind);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task No_token_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Garbage_token_returns_401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
