using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySelf.Infrastructure.Persistence;
using static MySelf.IntegrationTests.Auth.AuthTestHelpers;

namespace MySelf.IntegrationTests.Workouts;

/// <summary>
/// Starting and resuming a workout session (docs/02 "Starting a workout", Story 3A): the
/// session snapshot, the single-InProgress-session guard, and ownership.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class WorkoutSessionEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<Guid[]> TwoExerciseIdsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MySelfDbContext>();
        return await db.Exercises.OrderBy(e => e.Name).Select(e => e.Id).Take(2).ToArrayAsync();
    }

    /// <summary>Builds a program → day with one exercise carrying two prescribed sets, and
    /// returns the day's id.</summary>
    private async Task<Guid> CreateDayWithExercisesAsync(HttpClient client)
    {
        var ex = await TwoExerciseIdsAsync();

        var programRes = await client.PostAsJsonAsync("/api/v1/programs", new { name = "PPL", splitLabel = (string?)null });
        programRes.EnsureSuccessStatusCode();
        var programId = (await programRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var dayRes = await client.PostAsJsonAsync($"/api/v1/programs/{programId}/days", new { name = "Legs #1" });
        dayRes.EnsureSuccessStatusCode();
        var dayId = (await dayRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var body = new
        {
            name = "Legs #1",
            estimatedDurationMinutes = (int?)null,
            exercises = new[]
            {
                new
                {
                    exerciseId = ex[0],
                    sortOrder = 0,
                    supersetRef = (string?)null,
                    supersetMemberOrder = 0,
                    restSeconds = (int?)90,
                    notes = (string?)null,
                    sets = new[]
                    {
                        new { sortOrder = 0, kind = "Standard", isAmrap = false, targetToFailure = false, targetRepsMin = (int?)8, targetRepsMax = (int?)10, targetWeightKg = (double?)90.0, targetRir = (int?)2 },
                        new { sortOrder = 1, kind = "Standard", isAmrap = false, targetToFailure = false, targetRepsMin = (int?)8, targetRepsMax = (int?)10, targetWeightKg = (double?)90.0, targetRir = (int?)2 },
                    },
                },
            },
            supersets = Array.Empty<object>(),
        };
        var put = await client.PutAsJsonAsync($"/api/v1/workout-days/{dayId}", body);
        put.EnsureSuccessStatusCode();

        return dayId;
    }

    [Fact]
    public async Task Starting_from_a_day_snapshots_its_exercises_and_sets()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var dayId = await CreateDayWithExercisesAsync(client);

            var start = await client.PostAsJsonAsync("/api/v1/workout-sessions", new { dayId });
            Assert.Equal(HttpStatusCode.Created, start.StatusCode);

            var detail = await start.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("InProgress", detail.GetProperty("status").GetString());
            Assert.Equal("Legs #1", detail.GetProperty("dayName").GetString());
            Assert.Equal("PPL", detail.GetProperty("programName").GetString());

            var exercises = detail.GetProperty("exercises").EnumerateArray().ToList();
            var sets = exercises.Single().GetProperty("sets").EnumerateArray().ToList();
            Assert.Equal(2, sets.Count);
            Assert.Equal(8, sets[0].GetProperty("targetRepsMin").GetInt32());
            Assert.Null(sets[0].GetProperty("completedAt").GetString());

            var active = await client.GetAsync("/api/v1/workout-sessions/active");
            Assert.Equal(HttpStatusCode.OK, active.StatusCode);
            var activeDetail = await active.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(detail.GetProperty("id").GetGuid(), activeDetail.GetProperty("id").GetGuid());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Starting_ad_hoc_creates_a_session_with_no_exercises()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var start = await client.PostAsJsonAsync("/api/v1/workout-sessions", new { dayId = (Guid?)null });
            Assert.Equal(HttpStatusCode.Created, start.StatusCode);

            var detail = await start.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Null(detail.GetProperty("dayName").GetString());
            Assert.Empty(detail.GetProperty("exercises").EnumerateArray());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Starting_a_second_session_while_one_is_in_progress_is_rejected()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var first = await client.PostAsJsonAsync("/api/v1/workout-sessions", new { dayId = (Guid?)null });
            first.EnsureSuccessStatusCode();
            var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            var second = await client.PostAsJsonAsync("/api/v1/workout-sessions", new { dayId = (Guid?)null });
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

            var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(firstId, problem.GetProperty("sessionId").GetGuid());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Get_active_returns_404_when_nothing_is_in_progress()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var active = await client.GetAsync("/api/v1/workout-sessions/active");
            Assert.Equal(HttpStatusCode.NotFound, active.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Another_users_active_session_is_invisible()
    {
        var (ownerClient, ownerEmail) = await factory.RegisterAndAuthenticateAsync();
        var (otherClient, otherEmail) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var start = await ownerClient.PostAsJsonAsync("/api/v1/workout-sessions", new { dayId = (Guid?)null });
            start.EnsureSuccessStatusCode();

            var active = await otherClient.GetAsync("/api/v1/workout-sessions/active");
            Assert.Equal(HttpStatusCode.NotFound, active.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(ownerEmail, otherEmail);
        }
    }

    [Fact]
    public async Task Starting_from_another_users_day_returns_404()
    {
        var (ownerClient, ownerEmail) = await factory.RegisterAndAuthenticateAsync();
        var (otherClient, otherEmail) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var dayId = await CreateDayWithExercisesAsync(ownerClient);

            var start = await otherClient.PostAsJsonAsync("/api/v1/workout-sessions", new { dayId });
            Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(ownerEmail, otherEmail);
        }
    }
}
