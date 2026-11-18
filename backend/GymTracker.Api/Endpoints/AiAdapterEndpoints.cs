using System.Security.Claims;
using GymTracker.Api.Ai;
using GymTracker.Api.Auth;
using GymTracker.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GymTracker.Api.Endpoints;

public static class AiAdapterEndpoints
{
    public static IEndpointRouteBuilder MapAiAdapterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai")
            .RequireAuthorization();

        group.MapGet("/provider", GetProviderMetadata)
            .WithName("GetAiProviderMetadata")
            .WithSummary("Return configured AI provider metadata, model identifier, timeout, and cost-control limits");

        group.MapPost("/workout-draft-preview", GenerateWorkoutDraftPreviewAsync)
            .WithName("GenerateWorkoutDraftPreview")
            .WithSummary("Convert a natural-language workout description into an unpersisted structured proposal via the AI adapter");

        return app;
    }

    private static Ok<AiProviderMetadata> GetProviderMetadata(IAiProviderAdapter adapter) =>
        TypedResults.Ok(adapter.GetMetadata());

    private static async Task<Results<Ok<AiWorkoutDraftPreviewResponse>, BadRequest<AiWorkoutDraftErrorResponse>, JsonHttpResult<AiWorkoutDraftErrorResponse>>> GenerateWorkoutDraftPreviewAsync(
        AiWorkoutDraftPreviewRequest request,
        ClaimsPrincipal principal,
        IAiProviderAdapter adapter,
        GymTrackerDbContext db,
        CancellationToken cancellationToken)
    {
        var uid = principal.GetFirebaseUid()!;
        var originalText = request.DescriptionText ?? string.Empty;

        var catalogIds = await db.Exercises
            .AsNoTracking()
            .Select(e => e.Id)
            .ToHashSetAsync(StringComparer.Ordinal, cancellationToken);

        var structuredRequest = new AiStructuredRequest(
            OperationName: "WorkoutDraftPreview",
            UserUid: uid,
            SystemInstruction: "Convert the user's workout description into JSON matching WorkoutDraftProposal using only reviewed catalogue exercise IDs and kilograms for external load.",
            UserPrompt: originalText);

        var result = await adapter.GenerateStructuredAsync<WorkoutDraftProposal>(
            structuredRequest,
            proposal => ValidateWorkoutDraftProposal(proposal, catalogIds),
            cancellationToken);

        if (!result.IsSuccess || result.Proposal is null)
        {
            var errorBody = new AiWorkoutDraftErrorResponse(
                Error: result.ErrorMessage ?? "AI draft conversion failed.",
                FailureReason: result.FailureReason.ToString(),
                OriginalText: originalText,
                ManualFallbackAvailable: true);

            var statusCode = result.FailureReason switch
            {
                AiFailureReason.InputTooLarge => StatusCodes.Status400BadRequest,
                AiFailureReason.MalformedOutput or AiFailureReason.SchemaValidationFailed => StatusCodes.Status422UnprocessableEntity,
                AiFailureReason.QuotaExhausted or AiFailureReason.Disabled => StatusCodes.Status429TooManyRequests,
                AiFailureReason.Timeout => StatusCodes.Status504GatewayTimeout,
                _ => StatusCodes.Status503ServiceUnavailable
            };

            if (statusCode == StatusCodes.Status400BadRequest)
            {
                return TypedResults.BadRequest(errorBody);
            }

            return TypedResults.Json(errorBody, statusCode: statusCode);
        }

        return TypedResults.Ok(new AiWorkoutDraftPreviewResponse(
            OriginalText: originalText,
            Proposal: result.Proposal,
            RequiresUserConfirmation: true,
            Persisted: false,
            Provider: result.Provider,
            ModelId: result.ModelId,
            LatencyMs: result.LatencyMs));
    }

    public static string? ValidateWorkoutDraftProposal(
        WorkoutDraftProposal proposal,
        IReadOnlySet<string> allowedExerciseIds)
    {
        if (string.IsNullOrWhiteSpace(proposal.Summary))
        {
            return "Generated workout draft is missing a summary.";
        }

        if (proposal.Sets is null || proposal.Sets.Count == 0)
        {
            return "Generated workout draft must include at least one exercise set entry.";
        }

        foreach (var set in proposal.Sets)
        {
            if (string.IsNullOrWhiteSpace(set.ExerciseId) || !allowedExerciseIds.Contains(set.ExerciseId))
            {
                return $"Generated workout draft references unknown exercise '{set.ExerciseId}'.";
            }

            if (set.Sets <= 0 || set.Sets > 50)
            {
                return $"Generated workout draft has invalid set count ({set.Sets}) for '{set.ExerciseId}'.";
            }

            if (set.Repetitions <= 0 || set.Repetitions > 500)
            {
                return $"Generated workout draft has invalid repetition count ({set.Repetitions}) for '{set.ExerciseId}'.";
            }

            if (set.LoadKg is < 0 or > 1000)
            {
                return $"Generated workout draft has invalid load ({set.LoadKg} kg) for '{set.ExerciseId}'.";
            }
        }

        return null;
    }
}

public sealed record AiWorkoutDraftPreviewRequest(string DescriptionText);

public sealed record AiWorkoutDraftPreviewResponse(
    string OriginalText,
    WorkoutDraftProposal Proposal,
    bool RequiresUserConfirmation,
    bool Persisted,
    string Provider,
    string ModelId,
    long LatencyMs);

public sealed record AiWorkoutDraftErrorResponse(
    string Error,
    string FailureReason,
    string OriginalText,
    bool ManualFallbackAvailable);
