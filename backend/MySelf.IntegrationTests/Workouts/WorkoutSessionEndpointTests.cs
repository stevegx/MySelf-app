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

    /// <summary>Starts a session from a fresh day and returns (sessionId, first set id, its tracking mode).</summary>
    private async Task<(Guid SessionId, Guid SetId, string Mode)> StartWithASetAsync(HttpClient client)
    {
        var dayId = await CreateDayWithExercisesAsync(client);
        var start = await client.PostAsJsonAsync("/api/v1/workout-sessions", new { dayId });
        start.EnsureSuccessStatusCode();
        var detail = await start.Content.ReadFromJsonAsync<JsonElement>();
        var exercise = detail.GetProperty("exercises").EnumerateArray().First();
        return (
            detail.GetProperty("id").GetGuid(),
            exercise.GetProperty("sets").EnumerateArray().First().GetProperty("id").GetGuid(),
            exercise.GetProperty("trackingMode").GetString()!);
    }

    private static object LogBodyFor(Guid setLogId, string mode) => mode switch
    {
        "WeightAndReps" => new { setLogId, weightKg = 100.0, reps = 8, reachedFailure = false },
        "BodyweightPlusWeight" => new { setLogId, addedWeightKg = 20.0, reps = 8, reachedFailure = false },
        "AssistanceReps" => new { setLogId, assistanceKg = 15.0, reps = 8, reachedFailure = false },
        "Duration" => new { setLogId, durationSeconds = 60, reachedFailure = false },
        _ => new { setLogId, reps = 12, reachedFailure = false }, // BodyweightReps / RepsOnly
    };

    [Fact]
    public async Task Logging_a_set_with_valid_values_marks_it_complete()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var (sessionId, setId, mode) = await StartWithASetAsync(client);

            var res = await client.PostAsJsonAsync(
                $"/api/v1/workout-sessions/{sessionId}/set-logs", LogBodyFor(setId, mode));
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var set = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.NotEqual(JsonValueKind.Null, set.GetProperty("completedAt").ValueKind);
            Assert.Equal(JsonValueKind.Null, set.GetProperty("skippedAt").ValueKind);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Logging_a_set_with_no_values_is_rejected()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var (sessionId, setId, _) = await StartWithASetAsync(client);

            var res = await client.PostAsJsonAsync(
                $"/api/v1/workout-sessions/{sessionId}/set-logs", new { setLogId = setId, reachedFailure = false });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var body = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("errors").TryGetProperty("set", out _));
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Skipping_a_set_then_logging_it_clears_the_skip()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var (sessionId, setId, mode) = await StartWithASetAsync(client);

            var skip = await client.PostAsJsonAsync(
                $"/api/v1/workout-sessions/{sessionId}/skip-set", new { setLogId = setId, reason = "equipment" });
            Assert.Equal(HttpStatusCode.OK, skip.StatusCode);
            var skipped = await skip.Content.ReadFromJsonAsync<JsonElement>();
            Assert.NotEqual(JsonValueKind.Null, skipped.GetProperty("skippedAt").ValueKind);
            Assert.Equal("equipment", skipped.GetProperty("skippedReason").GetString());
            Assert.Equal(JsonValueKind.Null, skipped.GetProperty("completedAt").ValueKind);

            var log = await client.PostAsJsonAsync(
                $"/api/v1/workout-sessions/{sessionId}/set-logs", LogBodyFor(setId, mode));
            log.EnsureSuccessStatusCode();
            var relogged = await log.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Null, relogged.GetProperty("skippedAt").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, relogged.GetProperty("completedAt").ValueKind);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Completing_a_session_stamps_the_local_date_and_ends_the_active_session()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var (sessionId, setId, mode) = await StartWithASetAsync(client);
            await client.PostAsJsonAsync($"/api/v1/workout-sessions/{sessionId}/set-logs", LogBodyFor(setId, mode));

            var complete = await client.PostAsJsonAsync(
                $"/api/v1/workout-sessions/{sessionId}/complete", new { localDate = "2026-09-04", notes = "solid" });
            Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
            var done = await complete.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Completed", done.GetProperty("status").GetString());
            Assert.Equal("2026-09-04", done.GetProperty("performedOnLocalDate").GetString());
            Assert.Equal("solid", done.GetProperty("notes").GetString());

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/workout-sessions/active")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/workout-sessions/{sessionId}")).StatusCode);

            var again = await client.PostAsJsonAsync(
                $"/api/v1/workout-sessions/{sessionId}/complete", new { localDate = (string?)null, notes = (string?)null });
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Discarding_a_session_frees_the_slot_for_a_new_one()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var first = await client.PostAsJsonAsync("/api/v1/workout-sessions", new { dayId = (Guid?)null });
            var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            var discard = await client.PostAsJsonAsync($"/api/v1/workout-sessions/{firstId}/discard", new { });
            Assert.Equal(HttpStatusCode.NoContent, discard.StatusCode);

            var second = await client.PostAsJsonAsync("/api/v1/workout-sessions", new { dayId = (Guid?)null });
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Another_user_cannot_log_into_or_complete_your_session()
    {
        var (owner, ownerEmail) = await factory.RegisterAndAuthenticateAsync();
        var (other, otherEmail) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var (sessionId, setId, mode) = await StartWithASetAsync(owner);

            Assert.Equal(HttpStatusCode.NotFound,
                (await other.PostAsJsonAsync($"/api/v1/workout-sessions/{sessionId}/set-logs", LogBodyFor(setId, mode))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await other.PostAsJsonAsync($"/api/v1/workout-sessions/{sessionId}/complete", new { localDate = (string?)null, notes = (string?)null })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await other.GetAsync($"/api/v1/workout-sessions/{sessionId}")).StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(ownerEmail, otherEmail);
        }
    }

    [Fact]
    public async Task Completed_session_carries_a_summary_and_shows_up_in_history()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var (sessionId, setId, mode) = await StartWithASetAsync(client);
            await client.PostAsJsonAsync($"/api/v1/workout-sessions/{sessionId}/set-logs", LogBodyFor(setId, mode));
            await client.PostAsJsonAsync(
                $"/api/v1/workout-sessions/{sessionId}/complete", new { localDate = "2026-09-04", notes = (string?)null });

            var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/workout-sessions/{sessionId}");
            var summary = detail.GetProperty("summary");
            Assert.Equal(1, summary.GetProperty("completedSetCount").GetInt32());
            Assert.True(summary.GetProperty("durationSeconds").GetInt32() >= 0);

            var history = await client.GetFromJsonAsync<JsonElement>("/api/v1/workout-sessions?status=Completed");
            Assert.True(history.GetProperty("total").GetInt32() >= 1);
            var first = history.GetProperty("items").EnumerateArray().First();
            Assert.Equal(sessionId, first.GetProperty("id").GetGuid());
            Assert.Equal("2026-09-04", first.GetProperty("performedOnLocalDate").GetString());
            Assert.Equal(1, first.GetProperty("summary").GetProperty("completedSetCount").GetInt32());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task History_defaults_to_completed_and_can_filter_by_status()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            await client.PostAsJsonAsync("/api/v1/workout-sessions", new { dayId = (Guid?)null });

            var completedDefault = await client.GetFromJsonAsync<JsonElement>("/api/v1/workout-sessions");
            Assert.Equal(0, completedDefault.GetProperty("items").GetArrayLength());

            var inProgress = await client.GetFromJsonAsync<JsonElement>("/api/v1/workout-sessions?status=InProgress");
            Assert.Equal(1, inProgress.GetProperty("items").GetArrayLength());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task History_only_lists_your_own_sessions()
    {
        var (owner, ownerEmail) = await factory.RegisterAndAuthenticateAsync();
        var (other, otherEmail) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var (sessionId, setId, mode) = await StartWithASetAsync(owner);
            await owner.PostAsJsonAsync($"/api/v1/workout-sessions/{sessionId}/set-logs", LogBodyFor(setId, mode));
            await owner.PostAsJsonAsync(
                $"/api/v1/workout-sessions/{sessionId}/complete", new { localDate = "2026-09-04", notes = (string?)null });

            var otherHistory = await other.GetFromJsonAsync<JsonElement>("/api/v1/workout-sessions?status=Completed");
            Assert.Equal(0, otherHistory.GetProperty("items").GetArrayLength());
        }
        finally
        {
            await factory.DeleteUsersAsync(ownerEmail, otherEmail);
        }
    }
}
