using System.Net;
using System.Text;
using System.Text.Json;
using GymTracker.Api.Ai;
using GymTracker.Api.Endpoints;
using Microsoft.Extensions.Options;
using Xunit;

namespace GymTracker.Api.Tests;

public sealed class AiProviderAdapterTests
{
    private static readonly HashSet<string> AllowedExercises = new(StringComparer.Ordinal)
    {
        "barbell-back-squat",
        "barbell-bench-press"
    };

    [Fact]
    public async Task StubAdapter_ReturnsValidatedStructuredWorkoutProposal_AndRecordsMetadata()
    {
        var options = Options.Create(new AiProviderOptions
        {
            Provider = "GoogleGemini",
            ModelId = "gemini-2.5-flash",
            TimeoutSeconds = 30,
            MaxInputCharacters = 2000,
            MaxOutputTokens = 1024,
            MaxRequestsPerPeriod = 200,
            BudgetCapUsd = 5.00m,
            EstimatedCostPerRequestUsd = 0.0005m,
            ZeroDataRetention = true
        });
        var tracker = new AiBudgetTracker();
        var adapter = new StubAiProviderAdapter(options, tracker);

        var request = new AiStructuredRequest(
            OperationName: "WorkoutDraftPreview",
            UserUid: "user-ai-1",
            SystemInstruction: "Convert workout description to JSON.",
            UserPrompt: "I did three sets of ten squats at 40 kg");

        var result = await adapter.GenerateStructuredAsync<WorkoutDraftProposal>(
            request,
            proposal => AiAdapterEndpoints.ValidateWorkoutDraftProposal(proposal, AllowedExercises));

        Assert.True(result.IsSuccess);
        Assert.True(result.RequiresUserConfirmation);
        Assert.True(result.ManualFallbackAvailable);
        Assert.Equal(AiFailureReason.None, result.FailureReason);
        Assert.Equal("GoogleGemini", result.Provider);
        Assert.Equal("gemini-2.5-flash", result.ModelId);
        Assert.NotNull(result.Proposal);
        var set = Assert.Single(result.Proposal.Sets);
        Assert.Equal("barbell-back-squat", set.ExerciseId);
        Assert.Equal(3, set.Sets);
        Assert.Equal(10, set.Repetitions);
        Assert.Equal(40m, set.LoadKg);

        var metadata = adapter.GetMetadata();
        Assert.True(metadata.SupportsStructuredOutput);
        Assert.True(metadata.ZeroDataRetention);
        Assert.Equal(1, metadata.RequestsUsed);
        Assert.Equal(0.0005m, metadata.EstimatedSpendUsd);
    }

    [Fact]
    public async Task GeminiHttpAdapter_ParsesStructuredCandidateJson_AndValidatesAgainstCatalogue()
    {
        var geminiResponsePayload = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = """
                                {
                                  "summary": "Bench press strength session",
                                  "sets": [
                                    {
                                      "exerciseId": "barbell-bench-press",
                                      "sets": 4,
                                      "repetitions": 8,
                                      "loadKg": 60.0
                                    }
                                  ]
                                }
                                """
                            }
                        }
                    }
                }
            }
        });

        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(geminiResponsePayload, Encoding.UTF8, "application/json")
            })));

        var options = Options.Create(new AiProviderOptions
        {
            Provider = "GoogleGemini",
            ModelId = "gemini-2.5-flash",
            ApiKey = "configured-server-side-key",
            UseStub = false
        });
        var adapter = new GeminiAiProviderAdapter(httpClient, options, new AiBudgetTracker());

        var result = await adapter.GenerateStructuredAsync<WorkoutDraftProposal>(
            new AiStructuredRequest(
                "WorkoutDraftPreview",
                "user-ai-live",
                "Convert workout description to JSON.",
                "4 sets of 8 bench press at 60 kg"),
            proposal => AiAdapterEndpoints.ValidateWorkoutDraftProposal(proposal, AllowedExercises));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Proposal);
        var item = Assert.Single(result.Proposal.Sets);
        Assert.Equal("barbell-bench-press", item.ExerciseId);
        Assert.Equal(4, item.Sets);
        Assert.Equal(8, item.Repetitions);
        Assert.Equal(60m, item.LoadKg);
    }

    [Theory]
    [InlineData("{ not-valid-json }")]
    [InlineData("""{"summary":"x","sets":[],"unexpectedField":123}""")]
    public async Task Adapter_RejectsMalformedOrUnmappedJsonOutput(string rawJson)
    {
        var adapter = CreateStubAdapter((_, _) => Task.FromResult(rawJson));

        var result = await adapter.GenerateStructuredAsync<WorkoutDraftProposal>(
            CreateRequest(),
            proposal => AiAdapterEndpoints.ValidateWorkoutDraftProposal(proposal, AllowedExercises));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Proposal);
        Assert.Equal(AiFailureReason.MalformedOutput, result.FailureReason);
        Assert.True(result.ManualFallbackAvailable);
    }

    [Fact]
    public async Task Adapter_RejectsInventedExercisesAndNegativeLoads()
    {
        var inventedExerciseJson = """
        {
          "summary": "Invented exercise draft",
          "sets": [
            {
              "exerciseId": "invented-kettlebell-flip",
              "sets": 3,
              "repetitions": 10,
              "loadKg": 20
            }
          ]
        }
        """;

        var adapter = CreateStubAdapter((_, _) => Task.FromResult(inventedExerciseJson));

        var result = await adapter.GenerateStructuredAsync<WorkoutDraftProposal>(
            CreateRequest(),
            proposal => AiAdapterEndpoints.ValidateWorkoutDraftProposal(proposal, AllowedExercises));

        Assert.False(result.IsSuccess);
        Assert.Equal(AiFailureReason.SchemaValidationFailed, result.FailureReason);
        Assert.Contains("unknown exercise", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Adapter_TimesOutDelayedProvider_AndPreservesManualFallback()
    {
        var options = Options.Create(new AiProviderOptions { TimeoutSeconds = 1 });
        var adapter = new StubAiProviderAdapter(options, new AiBudgetTracker())
        {
            CustomHandler = async (_, token) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
                return "{}";
            }
        };

        var result = await adapter.GenerateStructuredAsync<WorkoutDraftProposal>(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(AiFailureReason.Timeout, result.FailureReason);
        Assert.True(result.ManualFallbackAvailable);
    }

    [Fact]
    public async Task Adapter_HonorsCallerCancellation()
    {
        var adapter = CreateStubAdapter(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            return "{}";
        });

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await adapter.GenerateStructuredAsync<WorkoutDraftProposal>(
            CreateRequest(),
            cancellationToken: cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(AiFailureReason.Cancelled, result.FailureReason);
        Assert.True(result.ManualFallbackAvailable);
    }

    [Fact]
    public async Task Adapter_EnforcesBudgetAndQuotaCaps_AndInputSizeBounds()
    {
        var options = Options.Create(new AiProviderOptions
        {
            MaxRequestsPerPeriod = 1,
            BudgetCapUsd = 0.001m,
            EstimatedCostPerRequestUsd = 0.001m,
            MaxInputCharacters = 50
        });
        var tracker = new AiBudgetTracker();
        var adapter = new StubAiProviderAdapter(options, tracker);

        var oversizedResult = await adapter.GenerateStructuredAsync<WorkoutDraftProposal>(
            CreateRequest(new string('a', 60)));
        Assert.False(oversizedResult.IsSuccess);
        Assert.Equal(AiFailureReason.InputTooLarge, oversizedResult.FailureReason);

        var firstResult = await adapter.GenerateStructuredAsync<WorkoutDraftProposal>(CreateRequest("3x10 squat 40kg"));
        Assert.True(firstResult.IsSuccess);

        var secondResult = await adapter.GenerateStructuredAsync<WorkoutDraftProposal>(CreateRequest("3x10 squat 40kg"));
        Assert.False(secondResult.IsSuccess);
        Assert.Equal(AiFailureReason.QuotaExhausted, secondResult.FailureReason);
        Assert.True(secondResult.ManualFallbackAvailable);
    }

    private static StubAiProviderAdapter CreateStubAdapter(
        Func<AiStructuredRequest, CancellationToken, Task<string>> handler)
    {
        var options = Options.Create(new AiProviderOptions());
        return new StubAiProviderAdapter(options, new AiBudgetTracker())
        {
            CustomHandler = handler
        };
    }

    private static AiStructuredRequest CreateRequest(string prompt = "3 sets of 10 squats at 40 kg") =>
        new("WorkoutDraftPreview", "user-test-1", "System prompt", prompt);

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
