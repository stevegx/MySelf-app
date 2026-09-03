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
/// Drives the program builder (docs/02, Story 3): programs → groups → variants → exercises →
/// set prescriptions → supersets, plus activation and ownership. Runs against the shared dev
/// database; the seeded exercise catalogue supplies real exercise ids.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class WorkoutBuilderEndpointTests(WebApplicationFactory<Program> factory, DatabaseFixture db)
    : DatabaseTest(db), IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<Guid[]> TwoExerciseIdsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MySelfDbContext>();
        return await db.Exercises.OrderBy(e => e.Name).Select(e => e.Id).Take(2).ToArrayAsync();
    }

    private static async Task<Guid> CreateProgramAsync(HttpClient client, string name = "PPL")
    {
        var res = await client.PostAsJsonAsync("/api/v1/programs", new { name, splitLabel = "Push/Pull/Legs" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> AddGroupAsync(HttpClient client, Guid programId, string name = "Legs")
    {
        var res = await client.PostAsJsonAsync($"/api/v1/programs/{programId}/groups", new { name });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> AddVariantAsync(HttpClient client, Guid groupId, string name = "Legs #1")
    {
        var res = await client.PostAsJsonAsync($"/api/v1/workout-groups/{groupId}/variants", new { name });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Create_add_group_variant_and_read_the_program_tree()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var programId = await CreateProgramAsync(client);
            var groupId = await AddGroupAsync(client, programId, "Legs");
            var variantId = await AddVariantAsync(client, groupId);

            var tree = await client.GetFromJsonAsync<JsonElement>($"/api/v1/programs/{programId}");
            var group = tree.GetProperty("groups").EnumerateArray().Single();
            Assert.Equal("Legs", group.GetProperty("name").GetString());
            var variant = group.GetProperty("variants").EnumerateArray().Single();
            Assert.Equal(variantId, variant.GetProperty("id").GetGuid());
            Assert.Equal(0, variant.GetProperty("exerciseCount").GetInt32());

            var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/programs");
            Assert.Equal(1, list.GetArrayLength());
            Assert.Equal(1, list[0].GetProperty("variantCount").GetInt32());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Put_variant_with_exercises_and_prescriptions_round_trips()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var ex = await TwoExerciseIdsAsync();
            var programId = await CreateProgramAsync(client);
            var groupId = await AddGroupAsync(client, programId);
            var variantId = await AddVariantAsync(client, groupId);

            var body = new
            {
                name = "Legs #1",
                estimatedDurationMinutes = 60,
                exercises = new[]
                {
                    new
                    {
                        exerciseId = ex[0],
                        sortOrder = 0,
                        supersetRef = (string?)null,
                        supersetMemberOrder = 0,
                        restSeconds = (int?)120,
                        notes = (string?)"warm up first",
                        sets = new[]
                        {
                            new { sortOrder = 0, kind = "Standard", isAmrap = false, targetToFailure = false, targetRepsMin = (int?)8, targetRepsMax = (int?)12, targetWeightKg = (double?)100.0, targetRir = (int?)2 },
                            new { sortOrder = 1, kind = "Standard", isAmrap = true, targetToFailure = false, targetRepsMin = (int?)null, targetRepsMax = (int?)null, targetWeightKg = (double?)90.0, targetRir = (int?)null },
                        },
                    },
                    new
                    {
                        exerciseId = ex[1],
                        sortOrder = 1,
                        supersetRef = (string?)null,
                        supersetMemberOrder = 0,
                        restSeconds = (int?)90,
                        notes = (string?)null,
                        sets = new[]
                        {
                            new { sortOrder = 0, kind = "Standard", isAmrap = false, targetToFailure = true, targetRepsMin = (int?)10, targetRepsMax = (int?)10, targetWeightKg = (double?)null, targetRir = (int?)null },
                        },
                    },
                },
                supersets = Array.Empty<object>(),
            };

            var put = await client.PutAsJsonAsync($"/api/v1/workout-variants/{variantId}", body);
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);

            var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/workout-variants/{variantId}");
            var exercises = detail.GetProperty("exercises").EnumerateArray().ToList();
            Assert.Equal(2, exercises.Count);
            Assert.Equal(ex[0], exercises[0].GetProperty("exerciseId").GetGuid());
            Assert.Equal(2, exercises[0].GetProperty("sets").GetArrayLength());
            Assert.True(exercises[0].GetProperty("sets")[1].GetProperty("isAmrap").GetBoolean());
            Assert.True(exercises[1].GetProperty("sets")[0].GetProperty("targetToFailure").GetBoolean());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Put_variant_with_a_superset_groups_the_members()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var ex = await TwoExerciseIdsAsync();
            var programId = await CreateProgramAsync(client);
            var groupId = await AddGroupAsync(client, programId);
            var variantId = await AddVariantAsync(client, groupId);

            var body = new
            {
                exercises = new[]
                {
                    new { exerciseId = ex[0], sortOrder = 0, supersetRef = "A", supersetMemberOrder = 0, restSeconds = (int?)null, notes = (string?)null, sets = Array.Empty<object>() },
                    new { exerciseId = ex[1], sortOrder = 1, supersetRef = "A", supersetMemberOrder = 1, restSeconds = (int?)null, notes = (string?)null, sets = Array.Empty<object>() },
                },
                supersets = new[] { new { @ref = "A", sortOrder = 0, restAfterRoundSeconds = 90 } },
            };

            var put = await client.PutAsJsonAsync($"/api/v1/workout-variants/{variantId}", body);
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);

            var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/workout-variants/{variantId}");
            var supersets = detail.GetProperty("supersets").EnumerateArray().ToList();
            Assert.Single(supersets);
            var supersetId = supersets[0].GetProperty("id").GetGuid();
            Assert.Equal(90, supersets[0].GetProperty("restAfterRoundSeconds").GetInt32());

            foreach (var e in detail.GetProperty("exercises").EnumerateArray())
            {
                Assert.Equal(supersetId, e.GetProperty("supersetGroupId").GetGuid());
            }
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Put_variant_replaces_the_previous_contents()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var ex = await TwoExerciseIdsAsync();
            var programId = await CreateProgramAsync(client);
            var groupId = await AddGroupAsync(client, programId);
            var variantId = await AddVariantAsync(client, groupId);

            object Exercise(Guid id, int order) => new
            {
                exerciseId = id, sortOrder = order, supersetRef = (string?)null, supersetMemberOrder = 0,
                restSeconds = (int?)null, notes = (string?)null,
                sets = new[] { new { sortOrder = 0, kind = "Standard", isAmrap = false, targetToFailure = false, targetRepsMin = 5, targetRepsMax = 5, targetWeightKg = (double?)null, targetRir = (int?)null } },
            };

            await client.PutAsJsonAsync($"/api/v1/workout-variants/{variantId}",
                new { exercises = new[] { Exercise(ex[0], 0), Exercise(ex[1], 1) }, supersets = Array.Empty<object>() });

            await client.PutAsJsonAsync($"/api/v1/workout-variants/{variantId}",
                new { exercises = new[] { Exercise(ex[1], 0) }, supersets = Array.Empty<object>() });

            var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/workout-variants/{variantId}");
            Assert.Equal(1, detail.GetProperty("exercises").GetArrayLength());
            Assert.Equal(ex[1], detail.GetProperty("exercises")[0].GetProperty("exerciseId").GetGuid());

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MySelfDbContext>();
            var orphanSets = await db.SetPrescriptions.CountAsync(s => s.VariantExercise.VariantId == variantId);
            Assert.Equal(1, orphanSets); // the two from the first PUT are gone
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Clone_deep_copies_the_tree_with_independent_ids_and_is_inactive()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var ex = await TwoExerciseIdsAsync();
            var programId = await CreateProgramAsync(client, "Original");
            var groupId = await AddGroupAsync(client, programId, "Push");
            var variantId = await AddVariantAsync(client, groupId, "Push #1");
            await client.PutAsJsonAsync($"/api/v1/workout-variants/{variantId}", new
            {
                exercises = new[]
                {
                    new { exerciseId = ex[0], sortOrder = 0, supersetRef = "A", supersetMemberOrder = 0, restSeconds = (int?)null, notes = (string?)"keep", sets = Array.Empty<object>() },
                    new { exerciseId = ex[1], sortOrder = 1, supersetRef = "A", supersetMemberOrder = 1, restSeconds = (int?)null, notes = (string?)null, sets = Array.Empty<object>() },
                },
                supersets = new[] { new { @ref = "A", sortOrder = 0, restAfterRoundSeconds = 75 } },
            });
            await client.PostAsync($"/api/v1/programs/{programId}/activate", null);

            var cloneRes = await client.PostAsync($"/api/v1/programs/{programId}/clone", null);
            Assert.Equal(HttpStatusCode.Created, cloneRes.StatusCode);
            var cloneSummary = await cloneRes.Content.ReadFromJsonAsync<JsonElement>();
            var cloneId = cloneSummary.GetProperty("id").GetGuid();
            Assert.NotEqual(programId, cloneId);
            Assert.Equal("Original (copy)", cloneSummary.GetProperty("name").GetString());
            Assert.False(cloneSummary.GetProperty("isActive").GetBoolean());

            var cloneTree = await client.GetFromJsonAsync<JsonElement>($"/api/v1/programs/{cloneId}");
            var cloneGroup = cloneTree.GetProperty("groups").EnumerateArray().Single();
            Assert.Equal("Push", cloneGroup.GetProperty("name").GetString());
            var cloneVariant = cloneGroup.GetProperty("variants").EnumerateArray().Single();
            var cloneVariantId = cloneVariant.GetProperty("id").GetGuid();
            Assert.NotEqual(variantId, cloneVariantId);

            var cloneVariantDetail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/workout-variants/{cloneVariantId}");
            var cloneExercises = cloneVariantDetail.GetProperty("exercises").EnumerateArray().ToList();
            Assert.Equal(2, cloneExercises.Count);
            Assert.Equal(ex[0], cloneExercises[0].GetProperty("exerciseId").GetGuid());
            Assert.Equal("keep", cloneExercises[0].GetProperty("notes").GetString());
            var cloneSuperset = cloneVariantDetail.GetProperty("supersets").EnumerateArray().Single();
            Assert.Equal(75, cloneSuperset.GetProperty("restAfterRoundSeconds").GetInt32());
            var cloneSupersetId = cloneSuperset.GetProperty("id").GetGuid();
            foreach (var e in cloneExercises)
            {
                Assert.Equal(cloneSupersetId, e.GetProperty("supersetGroupId").GetGuid());
            }

            // Editing the clone leaves the original untouched.
            await client.PutAsJsonAsync($"/api/v1/workout-variants/{cloneVariantId}",
                new { exercises = Array.Empty<object>(), supersets = Array.Empty<object>() });
            var originalDetail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/workout-variants/{variantId}");
            Assert.Equal(2, originalDetail.GetProperty("exercises").GetArrayLength());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Bulk_copy_appends_independent_exercises_to_the_destination()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var ex = await TwoExerciseIdsAsync();
            var programId = await CreateProgramAsync(client);
            var groupId = await AddGroupAsync(client, programId);
            var sourceId = await AddVariantAsync(client, groupId, "Source");
            var destId = await AddVariantAsync(client, groupId, "Dest");

            await client.PutAsJsonAsync($"/api/v1/workout-variants/{sourceId}", new
            {
                exercises = new[]
                {
                    new { exerciseId = ex[0], sortOrder = 0, supersetRef = (string?)null, supersetMemberOrder = 0, restSeconds = (int?)null, notes = (string?)null, sets = Array.Empty<object>() },
                    new { exerciseId = ex[1], sortOrder = 1, supersetRef = (string?)null, supersetMemberOrder = 0, restSeconds = (int?)null, notes = (string?)null, sets = Array.Empty<object>() },
                },
                supersets = Array.Empty<object>(),
            });

            var sourceDetail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/workout-variants/{sourceId}");
            var sourceExerciseIds = sourceDetail.GetProperty("exercises").EnumerateArray()
                .Select(e => e.GetProperty("id").GetGuid()).ToArray();

            var copy = await client.PostAsJsonAsync($"/api/v1/workout-variants/{destId}/exercises/bulk-copy",
                new { sourceVariantId = sourceId, variantExerciseIds = sourceExerciseIds });
            Assert.Equal(HttpStatusCode.OK, copy.StatusCode);

            var destDetail = await copy.Content.ReadFromJsonAsync<JsonElement>();
            var destExercises = destDetail.GetProperty("exercises").EnumerateArray().ToList();
            Assert.Equal(2, destExercises.Count);
            foreach (var e in destExercises)
            {
                Assert.DoesNotContain(e.GetProperty("id").GetGuid(), sourceExerciseIds);
            }

            var sourceAfter = await client.GetFromJsonAsync<JsonElement>($"/api/v1/workout-variants/{sourceId}");
            Assert.Equal(2, sourceAfter.GetProperty("exercises").GetArrayLength());
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Bulk_move_reparents_exercises_and_dissolves_a_broken_superset()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var ex = await TwoExerciseIdsAsync();
            var programId = await CreateProgramAsync(client);
            var groupId = await AddGroupAsync(client, programId);
            var sourceId = await AddVariantAsync(client, groupId, "Source");
            var destId = await AddVariantAsync(client, groupId, "Dest");

            await client.PutAsJsonAsync($"/api/v1/workout-variants/{sourceId}", new
            {
                exercises = new[]
                {
                    new { exerciseId = ex[0], sortOrder = 0, supersetRef = "A", supersetMemberOrder = 0, restSeconds = (int?)null, notes = (string?)null, sets = Array.Empty<object>() },
                    new { exerciseId = ex[1], sortOrder = 1, supersetRef = "A", supersetMemberOrder = 1, restSeconds = (int?)null, notes = (string?)null, sets = Array.Empty<object>() },
                },
                supersets = new[] { new { @ref = "A", sortOrder = 0, restAfterRoundSeconds = 60 } },
            });

            var sourceDetail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/workout-variants/{sourceId}");
            var firstExerciseId = sourceDetail.GetProperty("exercises")[0].GetProperty("id").GetGuid();

            var move = await client.PostAsJsonAsync($"/api/v1/workout-variants/{destId}/exercises/bulk-move",
                new { sourceVariantId = sourceId, variantExerciseIds = new[] { firstExerciseId } });
            Assert.Equal(HttpStatusCode.OK, move.StatusCode);

            var destDetail = await move.Content.ReadFromJsonAsync<JsonElement>();
            var movedExercise = destDetail.GetProperty("exercises").EnumerateArray().Single();
            Assert.Equal(firstExerciseId, movedExercise.GetProperty("id").GetGuid()); // identity preserved
            Assert.Equal(JsonValueKind.Null, movedExercise.GetProperty("supersetGroupId").ValueKind); // left its superset

            var sourceAfter = await client.GetFromJsonAsync<JsonElement>($"/api/v1/workout-variants/{sourceId}");
            Assert.Equal(1, sourceAfter.GetProperty("exercises").GetArrayLength());
            Assert.Equal(0, sourceAfter.GetProperty("supersets").GetArrayLength()); // 1 member left -> dissolved
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Activate_deactivates_the_previous_active_program()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var first = await CreateProgramAsync(client, "First");
            var second = await CreateProgramAsync(client, "Second");

            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/programs/{first}/activate", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/programs/{second}/activate", null)).StatusCode);

            var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/programs");
            var byId = list.EnumerateArray().ToDictionary(p => p.GetProperty("id").GetGuid(), p => p.GetProperty("isActive").GetBoolean());
            Assert.False(byId[first]);
            Assert.True(byId[second]);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Another_users_program_is_not_visible_or_editable()
    {
        var (alice, aliceEmail) = await factory.RegisterAndAuthenticateAsync();
        var (bob, bobEmail) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var programId = await CreateProgramAsync(alice);
            var groupId = await AddGroupAsync(alice, programId);
            var variantId = await AddVariantAsync(alice, groupId);

            Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/programs/{programId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync($"/api/v1/programs/{programId}/groups", new { name = "X" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/v1/programs/{programId}", new { name = "hijacked" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/programs/{programId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/v1/programs/{programId}/activate", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/v1/workout-groups/{groupId}", new { name = "X" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/workout-groups/{groupId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync($"/api/v1/workout-groups/{groupId}/variants", new { name = "X" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/workout-variants/{variantId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/v1/workout-variants/{variantId}", new { exercises = Array.Empty<object>(), supersets = Array.Empty<object>() })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/workout-variants/{variantId}")).StatusCode);
            Assert.Equal(0, (await bob.GetFromJsonAsync<JsonElement>("/api/v1/programs")).GetArrayLength());
        }
        finally
        {
            await factory.DeleteUsersAsync(aliceEmail, bobEmail);
        }
    }

    [Fact]
    public async Task Archive_hides_the_program_from_the_default_list()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var programId = await CreateProgramAsync(client);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/programs/{programId}")).StatusCode);

            Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/v1/programs")).GetArrayLength());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/programs/{programId}")).StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Archived_program_can_be_listed_and_restored()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var programId = await CreateProgramAsync(client, "Old plan");
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/programs/{programId}")).StatusCode);

            var archived = await client.GetFromJsonAsync<JsonElement>("/api/v1/programs/archived");
            Assert.Equal(1, archived.GetArrayLength());
            Assert.Equal(programId, archived[0].GetProperty("id").GetGuid());

            var restore = await client.PostAsync($"/api/v1/programs/{programId}/restore", null);
            Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);

            // Back in the default list, not active, and gone from the archived list.
            var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/programs");
            Assert.Equal(1, list.GetArrayLength());
            Assert.False(list[0].GetProperty("isActive").GetBoolean());
            Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/v1/programs/archived")).GetArrayLength());

            // Restoring an already-active (non-archived) program is a 404.
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/v1/programs/{programId}/restore", null)).StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Put_variant_rejects_an_unknown_exercise_and_a_lonely_superset()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var ex = await TwoExerciseIdsAsync();
            var programId = await CreateProgramAsync(client);
            var groupId = await AddGroupAsync(client, programId);
            var variantId = await AddVariantAsync(client, groupId);

            var unknown = await client.PutAsJsonAsync($"/api/v1/workout-variants/{variantId}", new
            {
                exercises = new[] { new { exerciseId = Guid.NewGuid(), sortOrder = 0, supersetRef = (string?)null, supersetMemberOrder = 0, restSeconds = (int?)null, notes = (string?)null, sets = Array.Empty<object>() } },
                supersets = Array.Empty<object>(),
            });
            Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

            var lonely = await client.PutAsJsonAsync($"/api/v1/workout-variants/{variantId}", new
            {
                exercises = new[] { new { exerciseId = ex[0], sortOrder = 0, supersetRef = "A", supersetMemberOrder = 0, restSeconds = (int?)null, notes = (string?)null, sets = Array.Empty<object>() } },
                supersets = new[] { new { @ref = "A", sortOrder = 0, restAfterRoundSeconds = 60 } },
            });
            Assert.Equal(HttpStatusCode.BadRequest, lonely.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Exercise_search_paginates_and_filters()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/exercises?pageSize=5");
            Assert.Equal(5, page.GetProperty("pageSize").GetInt32());
            Assert.True(page.GetProperty("items").GetArrayLength() <= 5);
            Assert.True(page.GetProperty("total").GetInt32() > 5);

            var filtered = await client.GetFromJsonAsync<JsonElement>("/api/v1/exercises?q=press&pageSize=50");
            foreach (var item in filtered.GetProperty("items").EnumerateArray())
            {
                Assert.Contains("press", item.GetProperty("name").GetString()!, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Put_variant_conflicts_when_the_program_row_version_is_stale()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var programId = await CreateProgramAsync(client);
            var groupId = await AddGroupAsync(client, programId);
            var variantId = await AddVariantAsync(client, groupId);

            var tree = await client.GetFromJsonAsync<JsonElement>($"/api/v1/programs/{programId}");
            var staleVersion = tree.GetProperty("rowVersion").GetUInt32();

            // First save with the current token succeeds and advances the program's xmin.
            var first = await client.PutAsJsonAsync($"/api/v1/workout-variants/{variantId}", new
            {
                exercises = Array.Empty<object>(),
                supersets = Array.Empty<object>(),
                rowVersion = staleVersion,
            });
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var fresh = await first.Content.ReadFromJsonAsync<JsonElement>();
            var freshVersion = fresh.GetProperty("programRowVersion").GetUInt32();
            Assert.NotEqual(staleVersion, freshVersion);

            // Re-using the now-stale token is rejected.
            var conflict = await client.PutAsJsonAsync($"/api/v1/workout-variants/{variantId}", new
            {
                exercises = Array.Empty<object>(),
                supersets = Array.Empty<object>(),
                rowVersion = staleVersion,
            });
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

            // The fresh token works again.
            var ok = await client.PutAsJsonAsync($"/api/v1/workout-variants/{variantId}", new
            {
                exercises = Array.Empty<object>(),
                supersets = Array.Empty<object>(),
                rowVersion = freshVersion,
            });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Put_program_conflicts_when_the_row_version_is_stale()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var programId = await CreateProgramAsync(client);
            var tree = await client.GetFromJsonAsync<JsonElement>($"/api/v1/programs/{programId}");
            var staleVersion = tree.GetProperty("rowVersion").GetUInt32();

            var first = await client.PutAsJsonAsync($"/api/v1/programs/{programId}",
                new { name = "Renamed once", rowVersion = staleVersion });
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

            var second = await client.PutAsJsonAsync($"/api/v1/programs/{programId}",
                new { name = "Renamed twice", rowVersion = staleVersion });
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

            // Omitting the token keeps the old last-write-wins behaviour.
            var noToken = await client.PutAsJsonAsync($"/api/v1/programs/{programId}", new { name = "Renamed anyway" });
            Assert.Equal(HttpStatusCode.NoContent, noToken.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Group_variant_reorder_round_trips_and_honours_the_row_version()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var programId = await CreateProgramAsync(client);
            var groupId = await AddGroupAsync(client, programId);
            var a = await AddVariantAsync(client, groupId, "A");
            var b = await AddVariantAsync(client, groupId, "B");

            var tree = await client.GetFromJsonAsync<JsonElement>($"/api/v1/programs/{programId}");
            var version = tree.GetProperty("rowVersion").GetUInt32();

            var reorder = await client.PutAsJsonAsync($"/api/v1/workout-groups/{groupId}",
                new { variantOrder = new[] { b, a }, rowVersion = version });
            Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

            var after = await client.GetFromJsonAsync<JsonElement>($"/api/v1/programs/{programId}");
            var orderedIds = after.GetProperty("groups")[0].GetProperty("variants").EnumerateArray()
                .Select(v => v.GetProperty("id").GetGuid()).ToArray();
            Assert.Equal(new[] { b, a }, orderedIds);

            // The token moved on with the first reorder.
            var stale = await client.PutAsJsonAsync($"/api/v1/workout-groups/{groupId}",
                new { variantOrder = new[] { a, b }, rowVersion = version });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Exercise_search_rejects_an_over_long_term()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var tooLong = new string('a', 101);
            var res = await client.GetAsync($"/api/v1/exercises?q={tooLong}");
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Put_variant_rejects_out_of_range_numbers()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var ex = await TwoExerciseIdsAsync();
            var programId = await CreateProgramAsync(client);
            var groupId = await AddGroupAsync(client, programId);
            var variantId = await AddVariantAsync(client, groupId);

            object ExerciseWithRest(int rest) => new
            {
                exerciseId = ex[0], sortOrder = 0, supersetRef = (string?)null, supersetMemberOrder = 0,
                restSeconds = (int?)rest, notes = (string?)null, sets = Array.Empty<object>(),
            };

            var badRest = await client.PutAsJsonAsync($"/api/v1/workout-variants/{variantId}",
                new { exercises = new[] { ExerciseWithRest(999_999) }, supersets = Array.Empty<object>() });
            Assert.Equal(HttpStatusCode.BadRequest, badRest.StatusCode);

            var badRir = await client.PutAsJsonAsync($"/api/v1/workout-variants/{variantId}", new
            {
                exercises = new[]
                {
                    new
                    {
                        exerciseId = ex[0], sortOrder = 0, supersetRef = (string?)null, supersetMemberOrder = 0,
                        restSeconds = (int?)null, notes = (string?)null,
                        sets = new[]
                        {
                            new { sortOrder = 0, kind = "Standard", isAmrap = false, targetToFailure = false, targetRepsMin = (int?)5, targetRepsMax = (int?)5, targetWeightKg = (double?)null, targetRir = (int?)99 },
                        },
                    },
                },
                supersets = Array.Empty<object>(),
            });
            Assert.Equal(HttpStatusCode.BadRequest, badRir.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Adding_a_31st_group_is_rejected()
    {
        var (client, email) = await factory.RegisterAndAuthenticateAsync();
        try
        {
            var programId = await CreateProgramAsync(client);
            for (var i = 0; i < 30; i++)
            {
                var ok = await client.PostAsJsonAsync($"/api/v1/programs/{programId}/groups", new { name = $"G{i}" });
                ok.EnsureSuccessStatusCode();
            }

            var over = await client.PostAsJsonAsync($"/api/v1/programs/{programId}/groups", new { name = "G31" });
            Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
        }
        finally
        {
            await factory.DeleteUsersAsync(email);
        }
    }

    [Fact]
    public async Task Builder_endpoints_require_a_token()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/programs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/exercises")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/programs", new { name = "X" })).StatusCode);
    }
}
