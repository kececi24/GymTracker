using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymTracker.Api.Auth;
using GymTracker.Api.Endpoints;
using Xunit;

namespace GymTracker.Api.Tests;

[Collection(PostgresApiCollection.Name)]
public sealed class IdentityAndProfileTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task UnauthenticatedRequests_AreRejectedWith401()
    {
        using var client = factory.CreateClient();

        var sessionResponse = await client.PostAsync("/api/auth/session", null);
        var profileResponse = await client.GetAsync("/api/users/user-unauth/profile");
        var updateResponse = await client.PutAsJsonAsync(
            "/api/users/user-unauth/profile",
            new UpdateProfileRequest("Can", 21, 180m, 78m, "Intermediate", "Strength", "Barbell", "Europe/Istanbul"));

        Assert.Equal(HttpStatusCode.Unauthorized, sessionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, profileResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, updateResponse.StatusCode);
    }

    [Fact]
    public async Task ExpiredAndMalformedTokens_AreRejectedWith401()
    {
        using var expiredJwtClient = factory.CreateClient();
        var expiredJwt = PostgresApiFactory.CreateFirebaseJwt(
            uid: "user-expired",
            email: "expired@example.com",
            expiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(-15));
        expiredJwtClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredJwt);

        var expiredResponse = await expiredJwtClient.GetAsync("/api/users/user-expired/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, expiredResponse.StatusCode);
        var expiredError = await expiredResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Contains("expired", expiredError?.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        using var wrongProjectClient = factory.CreateClient();
        var wrongProjectJwt = PostgresApiFactory.CreateFirebaseJwt(
            uid: "user-wrong-aud",
            email: "aud@example.com",
            projectId: "other-firebase-project");
        wrongProjectClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", wrongProjectJwt);

        var wrongProjectResponse = await wrongProjectClient.GetAsync("/api/users/user-wrong-aud/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongProjectResponse.StatusCode);
    }

    [Fact]
    public async Task EmailAndGoogleUsers_EstablishSessions_CompleteOnboarding_AndPersistProfiles()
    {
        var emailUid = $"email-{Guid.NewGuid():N}";
        var googleUid = $"google-{Guid.NewGuid():N}";

        using var emailClient = factory.CreateAuthenticatedClient(emailUid, "trainee@example.com", "password");
        using var googleClient = factory.CreateAuthenticatedClient(googleUid, "google.trainee@example.com", "google.com");

        var emailSession = await emailClient.PostAsync("/api/auth/session", null);
        Assert.Equal(HttpStatusCode.OK, emailSession.StatusCode);
        var emailInitialProfile = await emailSession.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(emailInitialProfile);
        Assert.Equal(emailUid, emailInitialProfile.FirebaseUid);
        Assert.Equal("password", emailInitialProfile.AuthProvider);
        Assert.False(emailInitialProfile.OnboardingCompleted);

        var googleSession = await googleClient.PostAsync("/api/auth/session", null);
        Assert.Equal(HttpStatusCode.OK, googleSession.StatusCode);
        var googleInitialProfile = await googleSession.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(googleInitialProfile);
        Assert.Equal(googleUid, googleInitialProfile.FirebaseUid);
        Assert.Equal("google.com", googleInitialProfile.AuthProvider);

        var onboardingResponse = await emailClient.PutAsJsonAsync(
            $"/api/users/{emailUid}/onboarding",
            new CompleteOnboardingRequest(
                DisplayName: "Can Kececi",
                Age: 22,
                HeightCm: 181.5m,
                WeightKg: 79.0m,
                ExperienceLevel: "Intermediate",
                PrimaryGoal: "Hypertrophy and strength",
                AvailableEquipment: "Barbell, Dumbbell, Cable",
                DisplayTimezone: "Europe/Istanbul"));
        Assert.Equal(HttpStatusCode.OK, onboardingResponse.StatusCode);

        var updatedResponse = await emailClient.PutAsJsonAsync(
            $"/api/users/{emailUid}/profile",
            new UpdateProfileRequest(
                DisplayName: "Can Sei Kececi",
                Age: 22,
                HeightCm: 181.5m,
                WeightKg: 80.5m,
                ExperienceLevel: "Advanced",
                PrimaryGoal: "Strength",
                AvailableEquipment: "Full Gym",
                DisplayTimezone: "Europe/Istanbul"));
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);

        var persistedProfile = await emailClient.GetFromJsonAsync<UserProfileResponse>($"/api/users/{emailUid}/profile");
        Assert.NotNull(persistedProfile);
        Assert.True(persistedProfile.OnboardingCompleted);
        Assert.Equal("Can Sei Kececi", persistedProfile.DisplayName);
        Assert.Equal(80.5m, persistedProfile.WeightKg);
        Assert.Equal("Advanced", persistedProfile.ExperienceLevel);
        Assert.Equal("Europe/Istanbul", persistedProfile.DisplayTimezone);
    }

    [Fact]
    public async Task NonOwnerRequests_AreRejectedWith403Forbidden()
    {
        var ownerUid = $"owner-{Guid.NewGuid():N}";
        var intruderUid = $"intruder-{Guid.NewGuid():N}";

        using var ownerClient = factory.CreateAuthenticatedClient(ownerUid);
        using var intruderClient = factory.CreateAuthenticatedClient(intruderUid);

        await ownerClient.PostAsync("/api/auth/session", null);

        var readOtherResponse = await intruderClient.GetAsync($"/api/users/{ownerUid}/profile");
        var updateOtherResponse = await intruderClient.PutAsJsonAsync(
            $"/api/users/{ownerUid}/profile",
            new UpdateProfileRequest("Intruder", 25, 175m, 70m, "Beginner", "Fitness", "Dumbbell", "UTC"));
        var onboardOtherResponse = await intruderClient.PutAsJsonAsync(
            $"/api/users/{ownerUid}/onboarding",
            new CompleteOnboardingRequest("Intruder", 25, 175m, 70m, "Beginner", "Fitness", "Dumbbell", "UTC"));

        Assert.Equal(HttpStatusCode.Forbidden, readOtherResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, updateOtherResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, onboardOtherResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidProfileValues_AreRejectedWith400BadRequest()
    {
        var uid = $"val-{Guid.NewGuid():N}";
        using var client = factory.CreateAuthenticatedClient(uid);

        var underageResponse = await client.PutAsJsonAsync(
            $"/api/users/{uid}/profile",
            new UpdateProfileRequest("Underage Trainee", 14, 170m, 65m, "Beginner", "General", null, "UTC"));

        Assert.Equal(HttpStatusCode.BadRequest, underageResponse.StatusCode);
    }
}
