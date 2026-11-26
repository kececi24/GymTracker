using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GymTracker.Api.Ai;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace GymTracker.Api.Tests;

[Collection(PostgresApiCollection.Name)]
public sealed class AiFeatureEndpointsTests(PostgresApiFactory factory)
{
    private const string WorkoutPreview = "/api/ai/workout-draft-preview";
    // Proposed WBS-8.4 routes derive their context from the authenticated account.
    private const string GoalSuggestion = "/api/ai/goal-suggestion";
    private const string ProgramProposal = "/api/ai/program-proposal";
    private const string ProgressInsight = "/api/ai/progress-insight";
    private const string ValidWorkoutJson = """
        {"summary":"Strength session","sets":[
          {"exerciseId":"barbell-back-squat","sets":2,"repetitions":8,"loadKg":40},
          {"exerciseId":"push-up","sets":1,"repetitions":12,"loadKg":null}
        ]}
        """;
    private const string GoalJson = """{"primaryGoal":"Do 15 consecutive push-ups"}""";
    private const string ProgramJson = """
        {"name":"Push-up progression","exercises":[
          {"exerciseId":"push-up","targetSets":3,"targetReps":8,"targetLoadKg":null}
        ]}
        """;
    private const string Goal = "Prepare for a push-up challenge";
    private const string Equipment = "Resistance bands and a mat";

    [Theory]
    [InlineData(WorkoutPreview)]
    [InlineData(GoalSuggestion)]
    [InlineData(ProgramProposal)]
    [InlineData(ProgressInsight)]
    public async Task AiRoutes_RequireAuthentication(string route)
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(route, new { descriptionText = "10 push-ups" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WorkoutPreview_ReturnsEditableCatalogueSets_AndOnlyExplicitWorkoutWritePersists()
    {
        const string description = "Two sets of eight barbell squats at 40 kg, then 12 push-ups.";
        using var app = StubProvider((_, _) => Task.FromResult(ValidWorkoutJson));
        using var client = NewClient(app);

        using var response = await client.PostAsJsonAsync(WorkoutPreview, new { descriptionText = description });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<WorkoutPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal(description, preview.OriginalText);
        Assert.True(preview.RequiresUserConfirmation);
        Assert.False(preview.Persisted);
        Assert.Equal(new DraftSet[] { new("barbell-back-squat", 2, 8, 40m), new("push-up", 1, 12, null) },
            preview.Proposal.Sets);
        Assert.Empty(await ReadArrayAsync(client, "/api/workouts"));

        // The user corrects the push-up count and explicitly submits the workout.
        await SaveWorkoutAsync(client, new("barbell-back-squat", 8, 40m),
            new("barbell-back-squat", 8, 40m), new("push-up", 10, null));
        var saved = Assert.Single(await ReadArrayAsync(client, "/api/workouts"));
        Assert.Equal(10, saved.GetProperty("sets")[2].GetProperty("repetitions").GetInt32());
    }

    [Theory]
    [InlineData("3 sets of 8 at 40 kg", """{"summary":"Session","sets":[{"exerciseId":"unknown-exercise","sets":3,"repetitions":8,"loadKg":40}]}""", "SchemaValidationFailed")]
    [InlineData("3 sets of 8 squats at 40 kg", """{"summary":"Session","sets":[{"exerciseId":"barbell-back-squat","sets":3,"repetitions":8,"loadKg":-40}]}""", "SchemaValidationFailed")]
    [InlineData("3 sets of 8 squats at 40", """{"summary":"Session","sets":[{"exerciseId":"barbell-back-squat","sets":3,"repetitions":8,"loadKg":40}]}""", "SchemaValidationFailed")]
    [InlineData("3 sets of 8 squats at 40 kg", "{invalid json}", "MalformedOutput")]
    public async Task WorkoutPreview_RejectsInvalidDraftAndPreservesText(
        string description, string providerJson, string failureReason)
    {
        using var app = StubProvider((_, _) => Task.FromResult(providerJson));
        using var client = NewClient(app);

        await AssertWorkoutErrorAsync(client, description, HttpStatusCode.UnprocessableEntity, failureReason);
    }

    [Fact]
    public async Task WorkoutPreview_ReportsTimeoutAndPreservesText()
    {
        using var app = StubProvider(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ValidWorkoutJson;
        }, new AiProviderOptions { TimeoutSeconds = 1 });
        using var client = NewClient(app);

        await AssertWorkoutErrorAsync(client, "12 push-ups", HttpStatusCode.GatewayTimeout, "Timeout");
    }

    [Fact]
    public async Task WorkoutPreview_ReportsQuotaExhaustionAndPreservesText()
    {
        using var app = StubProvider((_, _) => Task.FromResult(ValidWorkoutJson),
            new AiProviderOptions { MaxRequestsPerPeriod = 1 });
        using var client = NewClient(app);
        using var first = await client.PostAsJsonAsync(WorkoutPreview, new { descriptionText = "8 squats at 40 kg" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        await AssertWorkoutErrorAsync(client, "12 push-ups", HttpStatusCode.TooManyRequests, "QuotaExhausted");
    }

    [Theory]
    [InlineData(GoalSuggestion, GoalJson, "primaryGoal", "Do 15 consecutive push-ups")]
    [InlineData(ProgramProposal, ProgramJson, "name", "Push-up progression")]
    public async Task Planning_ReturnsProposalFromSavedProfile_WithoutChangingGoalsOrPrograms(
        string route, string providerJson, string proposalField, string proposedValue)
    {
        AiStructuredRequest? capturedRequest = null;
        using var app = StubProvider((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(providerJson);
        });
        var uid = NewUid();
        using var client = NewClient(app, uid);
        await SetUpProfileAsync(client, uid);

        var result = await PostProposalAsync(client, route);

        Assert.Equal(proposedValue, result.GetProperty("proposal").GetProperty(proposalField).GetString());
        Assert.True(result.GetProperty("requiresUserConfirmation").GetBoolean());
        Assert.NotNull(capturedRequest);
        Assert.Equal(uid, capturedRequest.UserUid);
        Assert.Contains(Goal, capturedRequest.UserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Equipment, capturedRequest.UserPrompt, StringComparison.OrdinalIgnoreCase);
        if (route == ProgramProposal)
        {
            Assert.Contains("Beginner", capturedRequest.UserPrompt, StringComparison.OrdinalIgnoreCase);
            var exercise = Assert.Single(result.GetProperty("proposal").GetProperty("exercises").EnumerateArray());
            Assert.Equal("push-up", exercise.GetProperty("exerciseId").GetString());
        }

        var profile = await client.GetFromJsonAsync<JsonElement>($"/api/users/{uid}/profile");
        Assert.Equal(Goal, profile.GetProperty("primaryGoal").GetString());
        Assert.Empty(await ReadArrayAsync(client, "/api/programs"));
    }

    [Theory]
    [InlineData(GoalSuggestion, "{invalid json}", "MalformedOutput")]
    [InlineData(ProgramProposal, """{"name":"Invalid plan","exercises":[{"exerciseId":"unknown-exercise","targetSets":3,"targetReps":8,"targetLoadKg":null}]}""", "SchemaValidationFailed")]
    [InlineData(ProgressInsight, """{"explanation":""}""", "SchemaValidationFailed")]
    public async Task Planning_RejectsInvalidProviderOutputAndOffersManualFallback(
        string route, string providerJson, string failureReason)
    {
        using var app = StubProvider((_, _) => Task.FromResult(providerJson));
        var uid = NewUid();
        using var client = NewClient(app, uid);
        await SetUpProfileAsync(client, uid);
        await SaveWorkoutAsync(client, new WorkoutSet("barbell-back-squat", 7, 43m));

        using var response = await client.PostAsJsonAsync(route, new { });

        await AssertAiErrorAsync(response, HttpStatusCode.UnprocessableEntity, failureReason);
        var profile = await client.GetFromJsonAsync<JsonElement>($"/api/users/{uid}/profile");
        Assert.Equal(Goal, profile.GetProperty("primaryGoal").GetString());
        Assert.Empty(await ReadArrayAsync(client, "/api/programs"));
        Assert.Single(await ReadArrayAsync(client, "/api/workouts"));
    }

    [Fact]
    public async Task ProgressInsight_UsesOnlyOwnersComputedMetrics_AndPreservesHistory()
    {
        AiStructuredRequest? capturedRequest = null;
        using var app = StubProvider((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult("""{"explanation":"Your squat training volume was 301 kg."}""");
        });
        var uid = NewUid();
        using var client = NewClient(app, uid);
        using var otherClient = NewClient(app);
        await SetUpProfileAsync(client, uid);
        await SaveWorkoutAsync(client, new WorkoutSet("barbell-back-squat", 7, 43m)); // 301 kg
        await SaveWorkoutAsync(otherClient, new WorkoutSet("barbell-bench-press", 11, 83m)); // 913 kg
        var before = Assert.Single(await ReadArrayAsync(client, "/api/workouts"));

        var result = await PostProposalAsync(client, ProgressInsight);

        Assert.Equal("Your squat training volume was 301 kg.",
            result.GetProperty("proposal").GetProperty("explanation").GetString());
        Assert.NotNull(capturedRequest);
        Assert.Equal(uid, capturedRequest.UserUid);
        var providerText = $"{capturedRequest.SystemInstruction}\n{capturedRequest.UserPrompt}";
        Assert.Contains("301", capturedRequest.UserPrompt);
        Assert.DoesNotContain("913", providerText);
        Assert.DoesNotContain("barbell-bench-press", providerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Goal, providerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Equipment, providerText, StringComparison.OrdinalIgnoreCase);
        var after = Assert.Single(await ReadArrayAsync(client, "/api/workouts"));
        Assert.True(JsonElement.DeepEquals(before, after));
    }

    private WebApplicationFactory<Program> StubProvider(
        Func<AiStructuredRequest, CancellationToken, Task<string>> handler, AiProviderOptions? options = null)
    {
        var adapter = new StubAiProviderAdapter(Options.Create(options ?? new AiProviderOptions()), new AiBudgetTracker())
        {
            CustomHandler = handler
        };
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IAiProviderAdapter>(adapter)));
    }

    private static string NewUid() => $"ai-feature-{Guid.NewGuid():N}";

    private static HttpClient NewClient(WebApplicationFactory<Program> app, string? uid = null)
    {
        uid ??= NewUid();
        var client = app.CreateClient();
        var jwt = PostgresApiFactory.CreateFirebaseJwt(uid, $"{uid}@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    private static async Task AssertWorkoutErrorAsync(
        HttpClient client, string description, HttpStatusCode status, string failureReason)
    {
        using var response = await client.PostAsJsonAsync(WorkoutPreview, new { descriptionText = description });
        var error = await AssertAiErrorAsync(response, status, failureReason);
        Assert.Equal(description, error.GetProperty("originalText").GetString());
        Assert.Empty(await ReadArrayAsync(client, "/api/workouts"));
    }

    private static async Task<JsonElement> AssertAiErrorAsync(
        HttpResponseMessage response, HttpStatusCode status, string failureReason)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(failureReason, error.GetProperty("failureReason").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("error").GetString()));
        Assert.True(error.GetProperty("manualFallbackAvailable").GetBoolean());
        return error;
    }

    private static async Task SetUpProfileAsync(HttpClient client, string uid)
    {
        using var response = await client.PutAsJsonAsync($"/api/users/{uid}/onboarding", new
        {
            displayName = "AI test trainee",
            experienceLevel = "Beginner",
            primaryGoal = Goal,
            availableEquipment = Equipment
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task SaveWorkoutAsync(HttpClient client, params WorkoutSet[] sets)
    {
        using var response = await client.PostAsJsonAsync("/api/workouts", new
        {
            performedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
            sets
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<JsonElement> PostProposalAsync(HttpClient client, string route)
    {
        using var response = await client.PostAsJsonAsync(route, new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement[]> ReadArrayAsync(HttpClient client, string route) =>
        Assert.IsType<JsonElement[]>(await client.GetFromJsonAsync<JsonElement[]>(route));

    private sealed record WorkoutPreviewResponse(
        string OriginalText, DraftProposal Proposal, bool RequiresUserConfirmation, bool Persisted);
    private sealed record DraftProposal(DraftSet[] Sets);
    private sealed record DraftSet(string ExerciseId, int Sets, int Repetitions, decimal? LoadKg);
    private sealed record WorkoutSet(string ExerciseId, int Repetitions, decimal? LoadKg);
}
