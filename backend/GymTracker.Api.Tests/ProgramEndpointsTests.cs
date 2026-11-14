using System.Net;
using System.Net.Http.Json;
using GymTracker.Api.Endpoints;
using Xunit;

namespace GymTracker.Api.Tests;

[Collection(PostgresApiCollection.Name)]
public sealed class ProgramEndpointsTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task CatalogueAndTemplates_ReturnSeededExercisesAndThreeTemplates()
    {
        var uid = $"cat-{Guid.NewGuid():N}";
        using var client = factory.CreateAuthenticatedClient(uid);

        var exercises = await client.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises");
        Assert.NotNull(exercises);
        Assert.True(exercises.Count >= 20);

        var legExercises = await client.GetFromJsonAsync<List<ExerciseDto>>("/api/exercises?muscleGroup=Legs");
        Assert.NotNull(legExercises);
        Assert.NotEmpty(legExercises);
        Assert.All(legExercises, ex => Assert.Equal("Legs", ex.PrimaryMuscleGroup));

        var templates = await client.GetFromJsonAsync<List<ProgramTemplateDto>>("/api/program-templates");
        Assert.NotNull(templates);
        Assert.True(templates.Count >= 3);
        Assert.All(templates, t => Assert.NotEmpty(t.Exercises));
    }

    [Fact]
    public async Task ProgramCrud_CreatesImmutableVersionSnapshotsOnEdit_AndPreservesPriorVersions()
    {
        var uid = $"prog-owner-{Guid.NewGuid():N}";
        using var client = factory.CreateAuthenticatedClient(uid);

        // 1. Create program from predefined template (version 1)
        var createResponse = await client.PostAsJsonAsync(
            "/api/programs",
            new UpsertProgramRequest(
                Name: "My Foundation Program",
                Description: "Initial 3-day routine",
                TemplateId: "full-body-foundation",
                Exercises: null));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdProgram = await createResponse.Content.ReadFromJsonAsync<ProgramDetailResponse>();
        Assert.NotNull(createdProgram);
        Assert.Equal(1, createdProgram.CurrentVersionNumber);
        Assert.Equal("full-body-foundation", createdProgram.SourceTemplateId);
        var v1VersionId = createdProgram.CurrentVersionId;
        var v1ExerciseCount = createdProgram.CurrentVersion.Exercises.Count;
        Assert.Equal(4, v1ExerciseCount);

        // 2. Edit program (creates immutable version 2)
        var editResponse = await client.PutAsJsonAsync(
            $"/api/programs/{createdProgram.Id}",
            new UpsertProgramRequest(
                Name: "My Foundation Program v2",
                Description: "Updated strength focus",
                TemplateId: null,
                Exercises:
                [
                    new ProgramExerciseInput("barbell-back-squat", TargetSets: 5, TargetReps: 5, TargetLoadKg: 90m, RestSeconds: 180),
                    new ProgramExerciseInput("overhead-barbell-press", TargetSets: 4, TargetReps: 6, TargetLoadKg: 45m, RestSeconds: 120)
                ]));
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);

        var updatedProgram = await editResponse.Content.ReadFromJsonAsync<ProgramDetailResponse>();
        Assert.NotNull(updatedProgram);
        Assert.Equal(2, updatedProgram.CurrentVersionNumber);
        Assert.NotEqual(v1VersionId, updatedProgram.CurrentVersionId);
        Assert.Equal(2, updatedProgram.Versions.Count);
        Assert.Equal(2, updatedProgram.CurrentVersion.Exercises.Count);
        Assert.Equal(90m, updatedProgram.CurrentVersion.Exercises[0].TargetLoadKg);

        // 3. Verify prior version v1 remains immutable and intact when referenced by number or ID
        var v1ByNumber = await client.GetFromJsonAsync<ProgramVersionResponse>(
            $"/api/programs/{createdProgram.Id}/versions/1");
        Assert.NotNull(v1ByNumber);
        Assert.Equal(v1VersionId, v1ByNumber.VersionId);
        Assert.Equal(1, v1ByNumber.VersionNumber);
        Assert.Equal("My Foundation Program", v1ByNumber.Name);
        Assert.Equal(4, v1ByNumber.Exercises.Count);
        Assert.Equal(40m, v1ByNumber.Exercises[0].TargetLoadKg);

        var v1ById = await client.GetFromJsonAsync<ProgramVersionResponse>(
            $"/api/program-versions/{v1VersionId}");
        Assert.NotNull(v1ById);
        Assert.Equal(1, v1ById.VersionNumber);
        Assert.Equal(4, v1ById.Exercises.Count);

        // 4. Archive program and verify v1 snapshot still resolves for historical workout references
        var deleteResponse = await client.DeleteAsync($"/api/programs/{createdProgram.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var activeList = await client.GetFromJsonAsync<List<ProgramSummaryResponse>>("/api/programs");
        Assert.NotNull(activeList);
        Assert.DoesNotContain(activeList, p => p.Id == createdProgram.Id);

        var v1AfterArchive = await client.GetFromJsonAsync<ProgramVersionResponse>(
            $"/api/program-versions/{v1VersionId}");
        Assert.NotNull(v1AfterArchive);
        Assert.Equal(v1VersionId, v1AfterArchive.VersionId);
    }

    [Fact]
    public async Task ProgramEndpoints_RejectInvalidSetsUnknownExercisesAndNonOwnerAccess()
    {
        var ownerUid = $"owner-prog-{Guid.NewGuid():N}";
        var otherUid = $"other-prog-{Guid.NewGuid():N}";
        using var ownerClient = factory.CreateAuthenticatedClient(ownerUid);
        using var otherClient = factory.CreateAuthenticatedClient(otherUid);

        var invalidExerciseResponse = await ownerClient.PostAsJsonAsync(
            "/api/programs",
            new UpsertProgramRequest(
                Name: "Invalid Exercise Program",
                Description: null,
                TemplateId: null,
                Exercises: [new ProgramExerciseInput("non-existent-exercise", 3, 10, 40m, 90)]));
        Assert.Equal(HttpStatusCode.BadRequest, invalidExerciseResponse.StatusCode);

        var invalidSetsResponse = await ownerClient.PostAsJsonAsync(
            "/api/programs",
            new UpsertProgramRequest(
                Name: "Zero Sets Program",
                Description: null,
                TemplateId: null,
                Exercises: [new ProgramExerciseInput("barbell-back-squat", 0, 10, 40m, 90)]));
        Assert.Equal(HttpStatusCode.BadRequest, invalidSetsResponse.StatusCode);

        var validCreate = await ownerClient.PostAsJsonAsync(
            "/api/programs",
            new UpsertProgramRequest(
                Name: "Owner Private Program",
                Description: null,
                TemplateId: "upper-lower-strength",
                Exercises: null));
        var created = await validCreate.Content.ReadFromJsonAsync<ProgramDetailResponse>();
        Assert.NotNull(created);

        // Non-owner requests must return 403 Forbidden
        var otherGet = await otherClient.GetAsync($"/api/programs/{created.Id}");
        var otherGetVersion = await otherClient.GetAsync($"/api/program-versions/{created.CurrentVersionId}");
        var otherUpdate = await otherClient.PutAsJsonAsync(
            $"/api/programs/{created.Id}",
            new UpsertProgramRequest(
                Name: "Hijacked",
                Description: null,
                TemplateId: null,
                Exercises: [new ProgramExerciseInput("push-up", 3, 15, null, 60)]));
        var otherDelete = await otherClient.DeleteAsync($"/api/programs/{created.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, otherGet.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherGetVersion.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherDelete.StatusCode);
    }

    [Fact]
    public async Task AiWorkoutDraftPreviewEndpoint_ReturnsUnpersistedValidatedProposal()
    {
        var uid = $"ai-ep-{Guid.NewGuid():N}";
        using var client = factory.CreateAuthenticatedClient(uid);

        var metadata = await client.GetFromJsonAsync<GymTracker.Api.Ai.AiProviderMetadata>("/api/ai/provider");
        Assert.NotNull(metadata);
        Assert.Equal("GoogleGemini", metadata.Provider);
        Assert.Equal("gemini-2.5-flash", metadata.ModelId);

        var response = await client.PostAsJsonAsync(
            "/api/ai/workout-draft-preview",
            new AiWorkoutDraftPreviewRequest("I did three sets of ten squats at 40 kg"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = await response.Content.ReadFromJsonAsync<AiWorkoutDraftPreviewResponse>();
        Assert.NotNull(preview);
        Assert.True(preview.RequiresUserConfirmation);
        Assert.False(preview.Persisted);
        Assert.Equal("I did three sets of ten squats at 40 kg", preview.OriginalText);
        Assert.Single(preview.Proposal.Sets);
    }
}
