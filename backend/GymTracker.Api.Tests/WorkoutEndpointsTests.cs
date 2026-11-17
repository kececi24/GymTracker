using System.Net;
using System.Net.Http.Json;
using GymTracker.Api.Endpoints;
using Xunit;

namespace GymTracker.Api.Tests;

// Test-first WBS-5.2 contract: /api/workouts stores a workout with an ordered list
// of individual sets. PUT replaces that list atomically (including removed sets).
// LoadKg uses the same canonical kilogram unit as program and AI draft contracts;
// null represents a bodyweight set. These wire DTOs deliberately live in tests
// until the workout API is implemented; no production workout types exist yet.
[Collection(PostgresApiCollection.Name)]
public sealed class WorkoutEndpointsTests(PostgresApiFactory factory)
{
    [Theory]
    [InlineData("GET", "/api/workouts")]
    [InlineData("POST", "/api/workouts")]
    [InlineData("GET", "/api/workouts/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/api/workouts/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/workouts/00000000-0000-0000-0000-000000000001")]
    public async Task WorkoutEndpoints_RequireAuthentication(string method, string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(ValidWorkout())
        };
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WorkoutCrud_PersistsEditsAndDeletesIndividualSets()
    {
        using var client = NewClient();
        var initial = ValidWorkout();
        var created = await CreateWorkoutAsync(client, initial);
        Assert.NotEqual(Guid.Empty, created.Id);
        await AssertPersistedAsync(client, created.Id, initial);

        // Edit a set, remove another, and add a bodyweight set in one replacement.
        var edited = initial with
        {
            PerformedAtUtc = initial.PerformedAtUtc.AddHours(1),
            Sets = [new("barbell-back-squat", 6, 50m), new("push-up", 15, null)]
        };
        using var update = await client.PutAsJsonAsync($"/api/workouts/{created.Id}", edited);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        await AssertPersistedAsync(client, created.Id, edited);

        var history = await client.GetFromJsonAsync<List<WorkoutResponse>>("/api/workouts");
        Assert.NotNull(history);
        Assert.Equal(created.Id, Assert.Single(history).Id);

        using var delete = await client.DeleteAsync($"/api/workouts/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var missing = await client.GetAsync($"/api/workouts/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var remaining = await client.GetFromJsonAsync<List<WorkoutResponse>>("/api/workouts");
        Assert.NotNull(remaining);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task WorkoutHistory_IsPrivate_AndNonOwnerCannotReadOrMutateRecords()
    {
        using var owner = NewClient();
        using var stranger = NewClient();
        var initial = ValidWorkout();
        var created = await CreateWorkoutAsync(owner, initial);
        var strangersWorkout = await CreateWorkoutAsync(stranger, initial);

        var history = await stranger.GetFromJsonAsync<List<WorkoutResponse>>("/api/workouts");
        Assert.NotNull(history);
        Assert.Equal(strangersWorkout.Id, Assert.Single(history).Id);
        using var read = await stranger.GetAsync($"/api/workouts/{created.Id}");
        using var update = await stranger.PutAsJsonAsync($"/api/workouts/{created.Id}",
            initial with { Sets = [new("push-up", 99, null)] });
        using var delete = await stranger.DeleteAsync($"/api/workouts/{created.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        await AssertPersistedAsync(owner, created.Id, initial);
    }

    [Fact]
    public async Task Workout_RetainsOriginalProgramVersion_AfterProgramEditAndArchive()
    {
        using var client = NewClient();
        var program = await CreateProgramAsync(client);
        var input = ValidWorkout() with { ProgramVersionId = program.CurrentVersionId };
        var workout = await CreateWorkoutAsync(client, input);

        using var update = await client.PutAsJsonAsync($"/api/programs/{program.Id}",
            new UpsertProgramRequest("Changed routine", null, null,
                [new ProgramExerciseInput("push-up", 4, 12, null, 60)]));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var edited = await update.Content.ReadFromJsonAsync<ProgramDetailResponse>();
        Assert.NotNull(edited);
        Assert.NotEqual(program.CurrentVersionId, edited.CurrentVersionId);
        await AssertPersistedAsync(client, workout.Id, input);

        using var archive = await client.DeleteAsync($"/api/programs/{program.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        await AssertPersistedAsync(client, workout.Id, input);
        var snapshot = await client.GetFromJsonAsync<ProgramVersionResponse>(
            $"/api/program-versions/{program.CurrentVersionId}");
        Assert.NotNull(snapshot);
        Assert.Equal(program.CurrentVersion.Name, snapshot.Name);
        Assert.Equal(program.CurrentVersion.Exercises.ToArray(), snapshot.Exercises.ToArray());
    }

    [Theory]
    [InlineData("empty-sets")]
    [InlineData("unknown-exercise")]
    [InlineData("zero-repetitions")]
    [InlineData("negative-repetitions")]
    [InlineData("negative-load")]
    [InlineData("unknown-version")]
    public async Task InvalidWorkoutWrites_LeaveNoPartialRecordOrSetChanges(string invalidField)
    {
        using var client = NewClient();
        var initial = ValidWorkout();
        var existing = await CreateWorkoutAsync(client, initial);
        // The first set is valid, so rejection of the second must not leave it saved.
        var first = new WorkoutSetInput("push-up", 12, null);
        var invalid = invalidField switch
        {
            "empty-sets" => initial with { Sets = [] },
            "unknown-exercise" => initial with { Sets = [first, new("missing-exercise", 10, 40m)] },
            "zero-repetitions" => initial with { Sets = [first, new("barbell-back-squat", 0, 40m)] },
            "negative-repetitions" => initial with { Sets = [first, new("barbell-back-squat", -1, 40m)] },
            "negative-load" => initial with { Sets = [first, new("barbell-back-squat", 10, -1m)] },
            "unknown-version" => initial with { ProgramVersionId = Guid.NewGuid(), Sets = [first] },
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField))
        };
        using var create = await client.PostAsJsonAsync("/api/workouts", invalid);
        using var update = await client.PutAsJsonAsync($"/api/workouts/{existing.Id}", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        await AssertPersistedAsync(client, existing.Id, initial);
        var history = await client.GetFromJsonAsync<List<WorkoutResponse>>("/api/workouts");
        Assert.NotNull(history);
        Assert.Equal(existing.Id, Assert.Single(history).Id);
    }

    [Fact]
    public async Task WorkoutWrites_RejectAnotherOwnersProgramVersion_WithoutChangingRecords()
    {
        using var owner = NewClient();
        using var stranger = NewClient();
        var program = await CreateProgramAsync(owner);
        var initial = ValidWorkout();
        var existing = await CreateWorkoutAsync(stranger, initial);
        var forbidden = initial with { ProgramVersionId = program.CurrentVersionId };
        using var create = await stranger.PostAsJsonAsync("/api/workouts", forbidden);
        using var update = await stranger.PutAsJsonAsync($"/api/workouts/{existing.Id}", forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        await AssertPersistedAsync(stranger, existing.Id, initial);
        var history = await stranger.GetFromJsonAsync<List<WorkoutResponse>>("/api/workouts");
        Assert.NotNull(history);
        Assert.Equal(existing.Id, Assert.Single(history).Id);
    }

    [Fact]
    public async Task WorkoutWrites_RejectLoadWithEmbeddedUnit_WithoutPersistingAnything()
    {
        using var client = NewClient();
        // Canonical loadKg accepts a numeric kilogram value, not a quantity string.
        using var response = await client.PostAsJsonAsync("/api/workouts", new
        {
            performedAtUtc = ValidWorkout().PerformedAtUtc,
            sets = new[] { new { exerciseId = "barbell-back-squat", repetitions = 10, loadKg = "40 lb" } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var history = await client.GetFromJsonAsync<List<WorkoutResponse>>("/api/workouts");
        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public async Task MissingWorkout_Returns404_ForReadUpdateAndDelete()
    {
        using var client = NewClient();
        var path = $"/api/workouts/{Guid.NewGuid()}";
        using var read = await client.GetAsync(path);
        using var update = await client.PutAsJsonAsync(path, ValidWorkout());
        using var delete = await client.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    private HttpClient NewClient() => factory.CreateAuthenticatedClient($"workout-{Guid.NewGuid():N}");

    private static WorkoutInput ValidWorkout() => new(
        new DateTimeOffset(2026, 11, 9, 18, 0, 0, TimeSpan.Zero), null,
        [new("barbell-back-squat", 10, 40m), new("barbell-back-squat", 8, 45m)]);

    private static async Task<ProgramDetailResponse> CreateProgramAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/programs",
            new UpsertProgramRequest("Original routine", null, "full-body-foundation", null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var program = await response.Content.ReadFromJsonAsync<ProgramDetailResponse>();
        Assert.NotNull(program);
        return program;
    }

    private static async Task<WorkoutResponse> CreateWorkoutAsync(HttpClient client, WorkoutInput input)
    {
        using var response = await client.PostAsJsonAsync("/api/workouts", input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var workout = await response.Content.ReadFromJsonAsync<WorkoutResponse>();
        Assert.NotNull(workout);
        Assert.Equal($"/api/workouts/{workout.Id}", response.Headers.Location?.OriginalString);
        return workout;
    }

    private static async Task AssertPersistedAsync(HttpClient client, Guid id, WorkoutInput expected)
    {
        var actual = await client.GetFromJsonAsync<WorkoutResponse>($"/api/workouts/{id}");
        Assert.NotNull(actual);
        Assert.Equal(id, actual.Id);
        Assert.Equal(expected.PerformedAtUtc, actual.PerformedAtUtc);
        Assert.Equal(expected.ProgramVersionId, actual.ProgramVersionId);
        Assert.Equal(expected.Sets.ToArray(), actual.Sets.ToArray());
    }

    public sealed record WorkoutSetInput(string ExerciseId, int Repetitions, decimal? LoadKg);
    public sealed record WorkoutInput(
        DateTimeOffset PerformedAtUtc, Guid? ProgramVersionId, IReadOnlyList<WorkoutSetInput> Sets);
    public sealed record WorkoutResponse(
        Guid Id, DateTimeOffset PerformedAtUtc, Guid? ProgramVersionId, IReadOnlyList<WorkoutSetInput> Sets);
}
