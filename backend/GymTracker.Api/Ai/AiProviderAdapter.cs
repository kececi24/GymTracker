using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace GymTracker.Api.Ai;

public sealed class AiProviderOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "GoogleGemini";
    public string ModelId { get; set; } = "gemini-2.5-flash";
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public string? ApiKey { get; set; }
    public bool UseStub { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxInputCharacters { get; set; } = 2000;
    public int MaxOutputTokens { get; set; } = 1024;
    public int MaxRequestsPerPeriod { get; set; } = 200;
    public decimal BudgetCapUsd { get; set; } = 5.00m;
    public decimal EstimatedCostPerRequestUsd { get; set; } = 0.0005m;
    public bool ZeroDataRetention { get; set; } = true;
}

public enum AiFailureReason
{
    None = 0,
    Disabled = 1,
    QuotaExhausted = 2,
    InputTooLarge = 3,
    Cancelled = 4,
    Timeout = 5,
    ProviderUnavailable = 6,
    MalformedOutput = 7,
    SchemaValidationFailed = 8
}

public sealed record AiProviderMetadata(
    string Provider,
    string ModelId,
    bool SupportsStructuredOutput,
    int TimeoutSeconds,
    int MaxInputCharacters,
    int MaxOutputTokens,
    int MaxRequestsPerPeriod,
    decimal BudgetCapUsd,
    decimal EstimatedCostPerRequestUsd,
    bool ZeroDataRetention,
    bool Enabled,
    int RequestsUsed,
    decimal EstimatedSpendUsd);

public sealed record AiStructuredRequest(
    string OperationName,
    string UserUid,
    string SystemInstruction,
    string UserPrompt,
    string? ResponseSchemaJson = null);

public sealed record AiAdapterResult<T>(
    bool IsSuccess,
    T? Proposal,
    bool RequiresUserConfirmation,
    bool ManualFallbackAvailable,
    string Provider,
    string ModelId,
    long LatencyMs,
    AiFailureReason FailureReason,
    string? ErrorMessage)
    where T : class
{
    public static AiAdapterResult<T> Success(
        T proposal,
        string provider,
        string modelId,
        long latencyMs) =>
        new(
            IsSuccess: true,
            Proposal: proposal,
            RequiresUserConfirmation: true,
            ManualFallbackAvailable: true,
            Provider: provider,
            ModelId: modelId,
            LatencyMs: latencyMs,
            FailureReason: AiFailureReason.None,
            ErrorMessage: null);

    public static AiAdapterResult<T> Failure(
        AiFailureReason reason,
        string errorMessage,
        string provider,
        string modelId,
        long latencyMs) =>
        new(
            IsSuccess: false,
            Proposal: null,
            RequiresUserConfirmation: true,
            ManualFallbackAvailable: true,
            Provider: provider,
            ModelId: modelId,
            LatencyMs: latencyMs,
            FailureReason: reason,
            ErrorMessage: errorMessage);
}

public sealed record WorkoutDraftProposal(
    string Summary,
    IReadOnlyList<WorkoutDraftSetProposal> Sets);

public sealed record WorkoutDraftSetProposal(
    string ExerciseId,
    int Sets,
    int Repetitions,
    decimal? LoadKg);

public interface IAiProviderAdapter
{
    AiProviderMetadata GetMetadata();

    Task<AiAdapterResult<T>> GenerateStructuredAsync<T>(
        AiStructuredRequest request,
        Func<T, string?>? domainValidator = null,
        CancellationToken cancellationToken = default)
        where T : class;
}

public sealed class AiBudgetTracker
{
    private readonly Lock _sync = new();
    private int _requestsUsed;
    private decimal _estimatedSpendUsd;

    public (int RequestsUsed, decimal EstimatedSpendUsd) GetUsage()
    {
        lock (_sync)
        {
            return (_requestsUsed, _estimatedSpendUsd);
        }
    }

    public bool TryReserveRequest(AiProviderOptions options, out string? denialMessage)
    {
        lock (_sync)
        {
            if (!options.Enabled)
            {
                denialMessage = "AI assistance is currently disabled by configuration. Please use manual entry.";
                return false;
            }

            if (_requestsUsed >= options.MaxRequestsPerPeriod)
            {
                denialMessage = "AI request quota has been reached for this period. Please continue with manual entry.";
                return false;
            }

            if (_estimatedSpendUsd + options.EstimatedCostPerRequestUsd > options.BudgetCapUsd)
            {
                denialMessage = "Approved AI budget cap has been reached. AI features are paused while manual workflows remain available.";
                return false;
            }

            _requestsUsed++;
            _estimatedSpendUsd += options.EstimatedCostPerRequestUsd;
            denialMessage = null;
            return true;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _requestsUsed = 0;
            _estimatedSpendUsd = 0m;
        }
    }
}

public static class AiStructuredOutputPipeline
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static AiProviderMetadata BuildMetadata(AiProviderOptions options, AiBudgetTracker tracker)
    {
        var (requestsUsed, spend) = tracker.GetUsage();
        return new AiProviderMetadata(
            Provider: options.Provider,
            ModelId: options.ModelId,
            SupportsStructuredOutput: true,
            TimeoutSeconds: options.TimeoutSeconds,
            MaxInputCharacters: options.MaxInputCharacters,
            MaxOutputTokens: options.MaxOutputTokens,
            MaxRequestsPerPeriod: options.MaxRequestsPerPeriod,
            BudgetCapUsd: options.BudgetCapUsd,
            EstimatedCostPerRequestUsd: options.EstimatedCostPerRequestUsd,
            ZeroDataRetention: options.ZeroDataRetention,
            Enabled: options.Enabled,
            RequestsUsed: requestsUsed,
            EstimatedSpendUsd: spend);
    }

    public static async Task<AiAdapterResult<T>> ExecuteAsync<T>(
        AiStructuredRequest request,
        AiProviderOptions options,
        AiBudgetTracker budgetTracker,
        Func<CancellationToken, Task<string>> rawJsonGenerator,
        Func<T, string?>? domainValidator,
        CancellationToken cancellationToken)
        where T : class
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(request.UserUid))
        {
            return AiAdapterResult<T>.Failure(
                AiFailureReason.SchemaValidationFailed,
                "AI requests must be scoped to an authenticated user account.",
                options.Provider,
                options.ModelId,
                stopwatch.ElapsedMilliseconds);
        }

        if (string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            return AiAdapterResult<T>.Failure(
                AiFailureReason.SchemaValidationFailed,
                "Prompt text cannot be empty. Please provide input or use manual entry.",
                options.Provider,
                options.ModelId,
                stopwatch.ElapsedMilliseconds);
        }

        if (request.UserPrompt.Length > options.MaxInputCharacters)
        {
            return AiAdapterResult<T>.Failure(
                AiFailureReason.InputTooLarge,
                $"Input exceeds the maximum allowed length of {options.MaxInputCharacters} characters.",
                options.Provider,
                options.ModelId,
                stopwatch.ElapsedMilliseconds);
        }

        if (!budgetTracker.TryReserveRequest(options, out var denialMessage))
        {
            var reason = options.Enabled ? AiFailureReason.QuotaExhausted : AiFailureReason.Disabled;
            return AiAdapterResult<T>.Failure(
                reason,
                denialMessage!,
                options.Provider,
                options.ModelId,
                stopwatch.ElapsedMilliseconds);
        }

        var timeoutSeconds = options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 30;
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        string rawJson;
        try
        {
            rawJson = await rawJsonGenerator(linkedCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AiAdapterResult<T>.Failure(
                AiFailureReason.Cancelled,
                "The AI request was cancelled. Your original draft text is preserved for manual entry.",
                options.Provider,
                options.ModelId,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return AiAdapterResult<T>.Failure(
                AiFailureReason.Timeout,
                $"The AI provider did not respond within {timeoutSeconds} seconds. Your draft is preserved and manual entry is available.",
                options.Provider,
                options.ModelId,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception)
        {
            return AiAdapterResult<T>.Failure(
                AiFailureReason.ProviderUnavailable,
                "The AI provider is temporarily unavailable. Your draft is preserved and manual entry is available.",
                options.Provider,
                options.ModelId,
                stopwatch.ElapsedMilliseconds);
        }

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return AiAdapterResult<T>.Failure(
                AiFailureReason.MalformedOutput,
                "The AI provider returned an empty response. Manual entry remains available.",
                options.Provider,
                options.ModelId,
                stopwatch.ElapsedMilliseconds);
        }

        T? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<T>(rawJson, SerializerOptions);
        }
        catch (JsonException)
        {
            return AiAdapterResult<T>.Failure(
                AiFailureReason.MalformedOutput,
                "The AI provider returned malformed structured output that failed schema parsing. Manual entry remains available.",
                options.Provider,
                options.ModelId,
                stopwatch.ElapsedMilliseconds);
        }

        if (parsed is null)
        {
            return AiAdapterResult<T>.Failure(
                AiFailureReason.MalformedOutput,
                "The AI provider returned a null proposal. Manual entry remains available.",
                options.Provider,
                options.ModelId,
                stopwatch.ElapsedMilliseconds);
        }

        if (domainValidator is not null)
        {
            var validationError = domainValidator(parsed);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return AiAdapterResult<T>.Failure(
                    AiFailureReason.SchemaValidationFailed,
                    validationError,
                    options.Provider,
                    options.ModelId,
                    stopwatch.ElapsedMilliseconds);
            }
        }

        return AiAdapterResult<T>.Success(
            parsed,
            options.Provider,
            options.ModelId,
            stopwatch.ElapsedMilliseconds);
    }
}

public sealed class StubAiProviderAdapter(
    IOptions<AiProviderOptions> optionsAccessor,
    AiBudgetTracker budgetTracker) : IAiProviderAdapter
{
    public Func<AiStructuredRequest, CancellationToken, Task<string>>? CustomHandler { get; set; }

    public AiProviderMetadata GetMetadata() =>
        AiStructuredOutputPipeline.BuildMetadata(optionsAccessor.Value, budgetTracker);

    public Task<AiAdapterResult<T>> GenerateStructuredAsync<T>(
        AiStructuredRequest request,
        Func<T, string?>? domainValidator = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var options = optionsAccessor.Value;
        return AiStructuredOutputPipeline.ExecuteAsync(
            request,
            options,
            budgetTracker,
            async token =>
            {
                token.ThrowIfCancellationRequested();
                if (CustomHandler is not null)
                {
                    return await CustomHandler(request, token);
                }

                if (typeof(T) == typeof(WorkoutDraftProposal))
                {
                    var sample = new WorkoutDraftProposal(
                        Summary: "Parsed resistance session from natural-language description.",
                        Sets:
                        [
                            new WorkoutDraftSetProposal(
                                ExerciseId: "barbell-back-squat",
                                Sets: 3,
                                Repetitions: 10,
                                LoadKg: 40m)
                        ]);
                    return JsonSerializer.Serialize(sample);
                }

                throw new InvalidOperationException($"No default stub response registered for type {typeof(T).Name}.");
            },
            domainValidator,
            cancellationToken);
    }
}

public sealed class GeminiAiProviderAdapter(
    HttpClient httpClient,
    IOptions<AiProviderOptions> optionsAccessor,
    AiBudgetTracker budgetTracker) : IAiProviderAdapter
{
    public AiProviderMetadata GetMetadata() =>
        AiStructuredOutputPipeline.BuildMetadata(optionsAccessor.Value, budgetTracker);

    public Task<AiAdapterResult<T>> GenerateStructuredAsync<T>(
        AiStructuredRequest request,
        Func<T, string?>? domainValidator = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var options = optionsAccessor.Value;
        return AiStructuredOutputPipeline.ExecuteAsync(
            request,
            options,
            budgetTracker,
            async token =>
            {
                if (string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    throw new InvalidOperationException("Gemini API key is not configured.");
                }

                var endpoint = $"{options.Endpoint.TrimEnd('/')}/models/{options.ModelId}:generateContent";
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpRequest.Headers.Add("x-goog-api-key", options.ApiKey);
                httpRequest.Content = JsonContent.Create(new
                {
                    systemInstruction = new
                    {
                        parts = new[] { new { text = request.SystemInstruction } }
                    },
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[] { new { text = request.UserPrompt } }
                        }
                    },
                    generationConfig = new
                    {
                        responseMimeType = "application/json",
                        maxOutputTokens = options.MaxOutputTokens,
                        temperature = 0.1
                    }
                });

                using var response = await httpClient.SendAsync(httpRequest, token);
                response.EnsureSuccessStatusCode();

                using var doc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(token),
                    cancellationToken: token);

                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() == 0)
                {
                    return string.Empty;
                }

                var text = candidates[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text ?? string.Empty;
            },
            domainValidator,
            cancellationToken);
    }
}
