using System.Security.Claims;
using GymTracker.Api.Auth;
using GymTracker.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GymTracker.Api.Endpoints;

public static class IdentityAndProfileEndpoints
{
    private static readonly HashSet<string> AllowedExperienceLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Beginner",
        "Intermediate",
        "Advanced"
    };

    public static IEndpointRouteBuilder MapIdentityAndProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .RequireAuthorization();

        group.MapPost("/auth/session", EstablishSessionAsync)
            .WithName("EstablishSession")
            .WithSummary("Verify Firebase identity token and initialize or load the owner's account profile");

        group.MapGet("/users/{uid}/profile", GetProfileAsync)
            .WithName("GetUserProfile")
            .WithSummary("Get the authenticated owner's profile");

        group.MapPut("/users/{uid}/profile", UpdateProfileAsync)
            .WithName("UpdateUserProfile")
            .WithSummary("Update the authenticated owner's profile");

        group.MapPut("/users/{uid}/onboarding", CompleteOnboardingAsync)
            .WithName("CompleteUserOnboarding")
            .WithSummary("Complete onboarding for the authenticated owner");

        return app;
    }

    private static async Task<Ok<UserProfileResponse>> EstablishSessionAsync(
        ClaimsPrincipal principal,
        GymTrackerDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var uid = principal.GetFirebaseUid()!;
        var email = principal.GetEmail();
        var provider = principal.GetSignInProvider();
        var now = timeProvider.GetUtcNow();

        var account = await db.UserAccounts.SingleOrDefaultAsync(u => u.FirebaseUid == uid, cancellationToken);
        if (account is null)
        {
            account = new UserAccount
            {
                FirebaseUid = uid,
                Email = email,
                AuthProvider = provider,
                DisplayName = string.Empty,
                DisplayTimezone = "UTC",
                OnboardingCompleted = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.UserAccounts.Add(account);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                account.Email = email;
            }

            account.AuthProvider = provider;
            account.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(account));
    }

    private static async Task<Results<Ok<UserProfileResponse>, JsonHttpResult<ApiErrorResponse>>> GetProfileAsync(
        string uid,
        ClaimsPrincipal principal,
        GymTrackerDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!principal.IsOwner(uid))
        {
            return ForbiddenResult();
        }

        var account = await GetOrCreateOwnerAccountAsync(uid, principal, db, timeProvider, cancellationToken);
        return TypedResults.Ok(ToResponse(account));
    }

    private static async Task<Results<Ok<UserProfileResponse>, BadRequest<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>>> UpdateProfileAsync(
        string uid,
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        GymTrackerDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!principal.IsOwner(uid))
        {
            return ForbiddenResult();
        }

        var validationError = ValidateProfileFields(
            request.DisplayName,
            request.Age,
            request.HeightCm,
            request.WeightKg,
            request.ExperienceLevel,
            request.DisplayTimezone);

        if (validationError is not null)
        {
            return TypedResults.BadRequest(new ApiErrorResponse(validationError));
        }

        var account = await GetOrCreateOwnerAccountAsync(uid, principal, db, timeProvider, cancellationToken);
        account.DisplayName = request.DisplayName.Trim();
        account.Age = request.Age;
        account.HeightCm = request.HeightCm;
        account.WeightKg = request.WeightKg;
        account.ExperienceLevel = NormalizeExperienceLevel(request.ExperienceLevel);
        account.PrimaryGoal = request.PrimaryGoal?.Trim();
        account.AvailableEquipment = request.AvailableEquipment?.Trim();
        account.DisplayTimezone = string.IsNullOrWhiteSpace(request.DisplayTimezone)
            ? "UTC"
            : request.DisplayTimezone.Trim();
        account.UpdatedAtUtc = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(account));
    }

    private static async Task<Results<Ok<UserProfileResponse>, BadRequest<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>>> CompleteOnboardingAsync(
        string uid,
        CompleteOnboardingRequest request,
        ClaimsPrincipal principal,
        GymTrackerDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!principal.IsOwner(uid))
        {
            return ForbiddenResult();
        }

        if (string.IsNullOrWhiteSpace(request.ExperienceLevel))
        {
            return TypedResults.BadRequest(new ApiErrorResponse("Experience level is required to complete onboarding."));
        }

        if (string.IsNullOrWhiteSpace(request.PrimaryGoal))
        {
            return TypedResults.BadRequest(new ApiErrorResponse("Primary goal is required to complete onboarding."));
        }

        var validationError = ValidateProfileFields(
            request.DisplayName,
            request.Age,
            request.HeightCm,
            request.WeightKg,
            request.ExperienceLevel,
            request.DisplayTimezone);

        if (validationError is not null)
        {
            return TypedResults.BadRequest(new ApiErrorResponse(validationError));
        }

        var account = await GetOrCreateOwnerAccountAsync(uid, principal, db, timeProvider, cancellationToken);
        account.DisplayName = request.DisplayName.Trim();
        account.Age = request.Age;
        account.HeightCm = request.HeightCm;
        account.WeightKg = request.WeightKg;
        account.ExperienceLevel = NormalizeExperienceLevel(request.ExperienceLevel);
        account.PrimaryGoal = request.PrimaryGoal.Trim();
        account.AvailableEquipment = request.AvailableEquipment?.Trim();
        account.DisplayTimezone = string.IsNullOrWhiteSpace(request.DisplayTimezone)
            ? "UTC"
            : request.DisplayTimezone.Trim();
        account.OnboardingCompleted = true;
        account.UpdatedAtUtc = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(account));
    }

    private static async Task<UserAccount> GetOrCreateOwnerAccountAsync(
        string uid,
        ClaimsPrincipal principal,
        GymTrackerDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var account = await db.UserAccounts.SingleOrDefaultAsync(u => u.FirebaseUid == uid, cancellationToken);
        if (account is not null)
        {
            return account;
        }

        var now = timeProvider.GetUtcNow();
        account = new UserAccount
        {
            FirebaseUid = uid,
            Email = principal.GetEmail(),
            AuthProvider = principal.GetSignInProvider(),
            DisplayName = string.Empty,
            DisplayTimezone = "UTC",
            OnboardingCompleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.UserAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    private static string? ValidateProfileFields(
        string? displayName,
        int? age,
        decimal? heightCm,
        decimal? weightKg,
        string? experienceLevel,
        string? displayTimezone)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "Display name is required.";
        }

        if (displayName.Trim().Length > 120)
        {
            return "Display name must not exceed 120 characters.";
        }

        if (age is < 16 or > 120)
        {
            return "Age must be between 16 and 120.";
        }

        if (heightCm is <= 0 or > 300)
        {
            return "Height in cm must be greater than 0 and at most 300.";
        }

        if (weightKg is <= 0 or > 500)
        {
            return "Weight in kg must be greater than 0 and at most 500.";
        }

        if (!string.IsNullOrWhiteSpace(experienceLevel) && !AllowedExperienceLevels.Contains(experienceLevel.Trim()))
        {
            return "Experience level must be Beginner, Intermediate, or Advanced.";
        }

        if (!string.IsNullOrWhiteSpace(displayTimezone) && displayTimezone.Trim().Length > 64)
        {
            return "Display timezone must not exceed 64 characters.";
        }

        return null;
    }

    private static string? NormalizeExperienceLevel(string? experienceLevel)
    {
        if (string.IsNullOrWhiteSpace(experienceLevel))
        {
            return null;
        }

        var trimmed = experienceLevel.Trim();
        if (string.Equals(trimmed, "beginner", StringComparison.OrdinalIgnoreCase))
        {
            return "Beginner";
        }

        if (string.Equals(trimmed, "intermediate", StringComparison.OrdinalIgnoreCase))
        {
            return "Intermediate";
        }

        if (string.Equals(trimmed, "advanced", StringComparison.OrdinalIgnoreCase))
        {
            return "Advanced";
        }

        return trimmed;
    }

    private static JsonHttpResult<ApiErrorResponse> ForbiddenResult() =>
        TypedResults.Json(
            new ApiErrorResponse("You do not have permission to access another user's resource."),
            statusCode: StatusCodes.Status403Forbidden);

    private static UserProfileResponse ToResponse(UserAccount account) =>
        new(
            account.FirebaseUid,
            account.Email,
            account.AuthProvider,
            account.DisplayName,
            account.Age,
            account.HeightCm,
            account.WeightKg,
            account.ExperienceLevel,
            account.PrimaryGoal,
            account.AvailableEquipment,
            account.DisplayTimezone,
            account.OnboardingCompleted,
            account.CreatedAtUtc,
            account.UpdatedAtUtc);
}

public sealed record UpdateProfileRequest(
    string DisplayName,
    int? Age,
    decimal? HeightCm,
    decimal? WeightKg,
    string? ExperienceLevel,
    string? PrimaryGoal,
    string? AvailableEquipment,
    string? DisplayTimezone);

public sealed record CompleteOnboardingRequest(
    string DisplayName,
    int? Age,
    decimal? HeightCm,
    decimal? WeightKg,
    string ExperienceLevel,
    string PrimaryGoal,
    string? AvailableEquipment,
    string? DisplayTimezone);

public sealed record UserProfileResponse(
    string FirebaseUid,
    string? Email,
    string AuthProvider,
    string DisplayName,
    int? Age,
    decimal? HeightCm,
    decimal? WeightKg,
    string? ExperienceLevel,
    string? PrimaryGoal,
    string? AvailableEquipment,
    string DisplayTimezone,
    bool OnboardingCompleted,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
