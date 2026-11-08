using System.Security.Claims;
using GymTracker.Api.Auth;
using GymTracker.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GymTracker.Api.Endpoints;

public static class ProgramEndpoints
{
    public static IEndpointRouteBuilder MapProgramEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .RequireAuthorization();

        group.MapGet("/exercises", ListExercisesAsync)
            .WithName("ListExercises")
            .WithSummary("Browse reviewed catalogue exercises with optional primary muscle group filter");

        group.MapGet("/program-templates", ListTemplatesAsync)
            .WithName("ListProgramTemplates")
            .WithSummary("List predefined training program templates");

        group.MapGet("/programs", ListOwnedProgramsAsync)
            .WithName("ListPrograms")
            .WithSummary("List active training programs owned by the authenticated user");

        group.MapPost("/programs", CreateProgramAsync)
            .WithName("CreateProgram")
            .WithSummary("Create a new owner-scoped training program with initial version snapshot v1");

        group.MapGet("/programs/{programId:guid}", GetProgramAsync)
            .WithName("GetProgram")
            .WithSummary("Get an owned training program and its current version snapshot");

        group.MapGet("/programs/{programId:guid}/versions/{versionNumber:int}", GetProgramVersionByNumberAsync)
            .WithName("GetProgramVersionByNumber")
            .WithSummary("Get a specific immutable version snapshot of an owned training program");

        group.MapGet("/program-versions/{versionId:guid}", GetProgramVersionByIdAsync)
            .WithName("GetProgramVersionById")
            .WithSummary("Get an immutable program version snapshot by its version ID");

        group.MapPut("/programs/{programId:guid}", UpdateProgramAsync)
            .WithName("UpdateProgram")
            .WithSummary("Update an owned program by recording a new immutable version snapshot");

        group.MapDelete("/programs/{programId:guid}", ArchiveProgramAsync)
            .WithName("ArchiveProgram")
            .WithSummary("Archive an owned program while retaining immutable version snapshots for historical workouts");

        return app;
    }

    private static async Task<Ok<IReadOnlyList<ExerciseDto>>> ListExercisesAsync(
        string? muscleGroup,
        GymTrackerDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Exercises.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(muscleGroup))
        {
            var normalized = muscleGroup.Trim();
            query = query.Where(e => e.PrimaryMuscleGroup.ToLower() == normalized.ToLower());
        }

        var items = await query
            .OrderBy(e => e.PrimaryMuscleGroup)
            .ThenBy(e => e.Name)
            .Select(e => new ExerciseDto(
                e.Id,
                e.Name,
                e.PrimaryMuscleGroup,
                e.Category,
                e.DefaultEquipment,
                e.Description))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok<IReadOnlyList<ExerciseDto>>(items);
    }

    private static async Task<Ok<IReadOnlyList<ProgramTemplateDto>>> ListTemplatesAsync(
        GymTrackerDbContext db,
        CancellationToken cancellationToken)
    {
        var templates = await db.ProgramTemplates
            .AsNoTracking()
            .Include(t => t.Exercises)
            .ThenInclude(te => te.Exercise)
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);

        var response = templates.Select(t => new ProgramTemplateDto(
            t.Id,
            t.Name,
            t.Description,
            t.RecommendedExperienceLevel,
            t.Exercises
                .OrderBy(e => e.DisplayOrder)
                .Select(e => new ProgramExerciseSnapshotDto(
                    e.ExerciseId,
                    e.Exercise?.Name ?? e.ExerciseId,
                    e.Exercise?.PrimaryMuscleGroup ?? string.Empty,
                    e.Exercise?.Category ?? "Resistance",
                    e.DisplayOrder,
                    e.TargetSets,
                    e.TargetReps,
                    e.TargetLoadKg,
                    e.RestSeconds))
                .ToList())).ToList();

        return TypedResults.Ok<IReadOnlyList<ProgramTemplateDto>>(response);
    }

    private static async Task<Ok<IReadOnlyList<ProgramSummaryResponse>>> ListOwnedProgramsAsync(
        ClaimsPrincipal principal,
        GymTrackerDbContext db,
        CancellationToken cancellationToken)
    {
        var uid = principal.GetFirebaseUid()!;
        var programs = await db.TrainingPrograms
            .AsNoTracking()
            .Where(p => p.OwnerUid == uid && !p.IsArchived)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .Select(p => new ProgramSummaryResponse(
                p.Id,
                p.OwnerUid,
                p.Name,
                p.Description,
                p.SourceTemplateId,
                p.CurrentVersionNumber,
                p.CurrentVersionId,
                p.CreatedAtUtc,
                p.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok<IReadOnlyList<ProgramSummaryResponse>>(programs);
    }

    private static async Task<Results<Created<ProgramDetailResponse>, BadRequest<ApiErrorResponse>>> CreateProgramAsync(
        UpsertProgramRequest request,
        ClaimsPrincipal principal,
        GymTrackerDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var uid = principal.GetFirebaseUid()!;
        var now = timeProvider.GetUtcNow();

        await EnsureOwnerAccountExistsAsync(uid, principal, db, now, cancellationToken);

        string? resolvedName = request.Name?.Trim();
        string? resolvedDescription = request.Description?.Trim();
        List<ProgramExerciseInput>? resolvedExercises = request.Exercises?.ToList();

        if (!string.IsNullOrWhiteSpace(request.TemplateId))
        {
            var templateId = request.TemplateId.Trim();
            var template = await db.ProgramTemplates
                .AsNoTracking()
                .Include(t => t.Exercises)
                .SingleOrDefaultAsync(t => t.Id == templateId, cancellationToken);

            if (template is null)
            {
                return TypedResults.BadRequest(new ApiErrorResponse($"Program template '{templateId}' does not exist."));
            }

            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                resolvedName = template.Name;
            }

            if (string.IsNullOrWhiteSpace(resolvedDescription))
            {
                resolvedDescription = template.Description;
            }

            if (resolvedExercises is null || resolvedExercises.Count == 0)
            {
                resolvedExercises = template.Exercises
                    .OrderBy(e => e.DisplayOrder)
                    .Select(e => new ProgramExerciseInput(
                        e.ExerciseId,
                        e.TargetSets,
                        e.TargetReps,
                        e.TargetLoadKg,
                        e.RestSeconds))
                    .ToList();
            }
        }

        var (validationError, exerciseMap) = await ValidateProgramInputAsync(resolvedName, resolvedExercises, db, cancellationToken);
        if (validationError is not null)
        {
            return TypedResults.BadRequest(new ApiErrorResponse(validationError));
        }

        var programId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var version = BuildProgramVersion(
            versionId,
            programId,
            versionNumber: 1,
            resolvedName!,
            resolvedDescription,
            resolvedExercises!,
            now);

        var program = new TrainingProgram
        {
            Id = programId,
            OwnerUid = uid,
            Name = resolvedName!,
            Description = resolvedDescription,
            SourceTemplateId = request.TemplateId?.Trim(),
            CurrentVersionNumber = 1,
            CurrentVersionId = versionId,
            IsArchived = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Versions = [version]
        };

        db.TrainingPrograms.Add(program);
        await db.SaveChangesAsync(cancellationToken);

        var detail = BuildProgramDetailResponse(program, version, exerciseMap);
        return TypedResults.Created($"/api/programs/{program.Id}", detail);
    }

    private static async Task<Results<Ok<ProgramDetailResponse>, NotFound<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>>> GetProgramAsync(
        Guid programId,
        ClaimsPrincipal principal,
        GymTrackerDbContext db,
        CancellationToken cancellationToken)
    {
        var program = await db.TrainingPrograms
            .AsNoTracking()
            .Include(p => p.Versions)
            .ThenInclude(v => v.Exercises)
            .ThenInclude(e => e.Exercise)
            .SingleOrDefaultAsync(p => p.Id == programId && !p.IsArchived, cancellationToken);

        if (program is null)
        {
            return TypedResults.NotFound(new ApiErrorResponse("Program not found."));
        }

        if (!principal.IsOwner(program.OwnerUid))
        {
            return ForbiddenResult();
        }

        var currentVersion = program.Versions
            .OrderByDescending(v => v.VersionNumber)
            .First();

        return TypedResults.Ok(ToProgramDetailResponse(program, currentVersion));
    }

    private static async Task<Results<Ok<ProgramVersionResponse>, NotFound<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>>> GetProgramVersionByNumberAsync(
        Guid programId,
        int versionNumber,
        ClaimsPrincipal principal,
        GymTrackerDbContext db,
        CancellationToken cancellationToken)
    {
        var program = await db.TrainingPrograms
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == programId, cancellationToken);

        if (program is null)
        {
            return TypedResults.NotFound(new ApiErrorResponse("Program not found."));
        }

        if (!principal.IsOwner(program.OwnerUid))
        {
            return ForbiddenResult();
        }

        var version = await db.ProgramVersions
            .AsNoTracking()
            .Include(v => v.Exercises)
            .ThenInclude(e => e.Exercise)
            .SingleOrDefaultAsync(v => v.ProgramId == programId && v.VersionNumber == versionNumber, cancellationToken);

        if (version is null)
        {
            return TypedResults.NotFound(new ApiErrorResponse($"Version {versionNumber} was not found for this program."));
        }

        return TypedResults.Ok(ToProgramVersionResponse(version));
    }

    private static async Task<Results<Ok<ProgramVersionResponse>, NotFound<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>>> GetProgramVersionByIdAsync(
        Guid versionId,
        ClaimsPrincipal principal,
        GymTrackerDbContext db,
        CancellationToken cancellationToken)
    {
        var version = await db.ProgramVersions
            .AsNoTracking()
            .Include(v => v.Program)
            .Include(v => v.Exercises)
            .ThenInclude(e => e.Exercise)
            .SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken);

        if (version?.Program is null)
        {
            return TypedResults.NotFound(new ApiErrorResponse("Program version not found."));
        }

        if (!principal.IsOwner(version.Program.OwnerUid))
        {
            return ForbiddenResult();
        }

        return TypedResults.Ok(ToProgramVersionResponse(version));
    }

    private static async Task<Results<Ok<ProgramDetailResponse>, BadRequest<ApiErrorResponse>, NotFound<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>>> UpdateProgramAsync(
        Guid programId,
        UpsertProgramRequest request,
        ClaimsPrincipal principal,
        GymTrackerDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var program = await db.TrainingPrograms
            .Include(p => p.Versions)
            .SingleOrDefaultAsync(p => p.Id == programId && !p.IsArchived, cancellationToken);

        if (program is null)
        {
            return TypedResults.NotFound(new ApiErrorResponse("Program not found."));
        }

        if (!principal.IsOwner(program.OwnerUid))
        {
            return ForbiddenResult();
        }

        var resolvedName = request.Name?.Trim();
        var resolvedDescription = request.Description?.Trim();
        var resolvedExercises = request.Exercises?.ToList();

        var (validationError, exerciseMap) = await ValidateProgramInputAsync(resolvedName, resolvedExercises, db, cancellationToken);
        if (validationError is not null)
        {
            return TypedResults.BadRequest(new ApiErrorResponse(validationError));
        }

        var now = timeProvider.GetUtcNow();
        var nextVersionNumber = program.CurrentVersionNumber + 1;
        var nextVersionId = Guid.NewGuid();

        var newVersion = BuildProgramVersion(
            nextVersionId,
            program.Id,
            nextVersionNumber,
            resolvedName!,
            resolvedDescription,
            resolvedExercises!,
            now);

        program.Name = resolvedName!;
        program.Description = resolvedDescription;
        program.CurrentVersionNumber = nextVersionNumber;
        program.CurrentVersionId = nextVersionId;
        program.UpdatedAtUtc = now;

        db.ProgramVersions.Add(newVersion);
        await db.SaveChangesAsync(cancellationToken);

        var detail = BuildProgramDetailResponse(program, newVersion, exerciseMap);
        return TypedResults.Ok(detail);
    }

    private static async Task<Results<NoContent, NotFound<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>>> ArchiveProgramAsync(
        Guid programId,
        ClaimsPrincipal principal,
        GymTrackerDbContext db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var program = await db.TrainingPrograms
            .SingleOrDefaultAsync(p => p.Id == programId && !p.IsArchived, cancellationToken);

        if (program is null)
        {
            return TypedResults.NotFound(new ApiErrorResponse("Program not found."));
        }

        if (!principal.IsOwner(program.OwnerUid))
        {
            return ForbiddenResult();
        }

        program.IsArchived = true;
        program.UpdatedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task EnsureOwnerAccountExistsAsync(
        string uid,
        ClaimsPrincipal principal,
        GymTrackerDbContext db,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var exists = await db.UserAccounts.AnyAsync(u => u.FirebaseUid == uid, cancellationToken);
        if (!exists)
        {
            db.UserAccounts.Add(new UserAccount
            {
                FirebaseUid = uid,
                Email = principal.GetEmail(),
                AuthProvider = principal.GetSignInProvider(),
                DisplayName = string.Empty,
                DisplayTimezone = "UTC",
                OnboardingCompleted = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
    }

    private static async Task<(string? Error, Dictionary<string, Exercise> ExercisesById)> ValidateProgramInputAsync(
        string? name,
        IReadOnlyList<ProgramExerciseInput>? exercises,
        GymTrackerDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ("Program name is required.", []);
        }

        if (name.Length > 120)
        {
            return ("Program name must not exceed 120 characters.", []);
        }

        if (exercises is null || exercises.Count == 0)
        {
            return ("A program must contain at least one exercise.", []);
        }

        if (exercises.Count > 30)
        {
            return ("A program must not exceed 30 exercises.", []);
        }

        var requestedIds = exercises
            .Select(e => e.ExerciseId?.Trim() ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var catalogExercises = await db.Exercises
            .AsNoTracking()
            .Where(e => requestedIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, StringComparer.Ordinal, cancellationToken);

        for (var i = 0; i < exercises.Count; i++)
        {
            var item = exercises[i];
            var exerciseId = item.ExerciseId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(exerciseId) || !catalogExercises.TryGetValue(exerciseId, out var catalogExercise))
            {
                return ($"Exercise '{exerciseId}' at position {i + 1} is not in the reviewed catalogue.", []);
            }

            if (item.TargetSets <= 0 || item.TargetSets > 50)
            {
                return ($"Target sets for '{catalogExercise.Name}' must be between 1 and 50.", []);
            }

            if (item.TargetReps <= 0 || item.TargetReps > 500)
            {
                return ($"Target repetitions for '{catalogExercise.Name}' must be between 1 and 500.", []);
            }

            if (item.TargetLoadKg is < 0 or > 1000)
            {
                return ($"Target load (kg) for '{catalogExercise.Name}' must be between 0 and 1000.", []);
            }

            if (item.RestSeconds is < 0 or > 3600)
            {
                return ($"Rest seconds for '{catalogExercise.Name}' must be between 0 and 3600.", []);
            }
        }

        return (null, catalogExercises);
    }

    private static ProgramVersion BuildProgramVersion(
        Guid versionId,
        Guid programId,
        int versionNumber,
        string name,
        string? description,
        IReadOnlyList<ProgramExerciseInput> exercises,
        DateTimeOffset createdAtUtc)
    {
        var items = exercises.Select((item, index) => new ProgramVersionExercise
        {
            Id = Guid.NewGuid(),
            ProgramVersionId = versionId,
            ExerciseId = item.ExerciseId.Trim(),
            DisplayOrder = index + 1,
            TargetSets = item.TargetSets,
            TargetReps = item.TargetReps,
            TargetLoadKg = item.TargetLoadKg,
            RestSeconds = item.RestSeconds
        }).ToList();

        return new ProgramVersion
        {
            Id = versionId,
            ProgramId = programId,
            VersionNumber = versionNumber,
            Name = name,
            Description = description,
            CreatedAtUtc = createdAtUtc,
            Exercises = items
        };
    }

    private static ProgramDetailResponse BuildProgramDetailResponse(
        TrainingProgram program,
        ProgramVersion currentVersion,
        IReadOnlyDictionary<string, Exercise> exerciseMap)
    {
        var currentVersionDto = new ProgramVersionResponse(
            currentVersion.Id,
            currentVersion.ProgramId,
            currentVersion.VersionNumber,
            currentVersion.Name,
            currentVersion.Description,
            currentVersion.CreatedAtUtc,
            currentVersion.Exercises
                .OrderBy(e => e.DisplayOrder)
                .Select(e =>
                {
                    exerciseMap.TryGetValue(e.ExerciseId, out var ex);
                    return new ProgramExerciseSnapshotDto(
                        e.ExerciseId,
                        ex?.Name ?? e.ExerciseId,
                        ex?.PrimaryMuscleGroup ?? string.Empty,
                        ex?.Category ?? "Resistance",
                        e.DisplayOrder,
                        e.TargetSets,
                        e.TargetReps,
                        e.TargetLoadKg,
                        e.RestSeconds);
                })
                .ToList());

        var versionInfos = program.Versions
            .OrderBy(v => v.VersionNumber)
            .Select(v => new ProgramVersionHeaderDto(v.Id, v.VersionNumber, v.Name, v.CreatedAtUtc))
            .ToList();

        return new ProgramDetailResponse(
            program.Id,
            program.OwnerUid,
            program.Name,
            program.Description,
            program.SourceTemplateId,
            program.CurrentVersionNumber,
            program.CurrentVersionId,
            program.CreatedAtUtc,
            program.UpdatedAtUtc,
            currentVersionDto,
            versionInfos);
    }

    private static ProgramDetailResponse ToProgramDetailResponse(
        TrainingProgram program,
        ProgramVersion currentVersion)
    {
        var currentVersionDto = ToProgramVersionResponse(currentVersion);
        var versionInfos = program.Versions
            .OrderBy(v => v.VersionNumber)
            .Select(v => new ProgramVersionHeaderDto(v.Id, v.VersionNumber, v.Name, v.CreatedAtUtc))
            .ToList();

        return new ProgramDetailResponse(
            program.Id,
            program.OwnerUid,
            program.Name,
            program.Description,
            program.SourceTemplateId,
            program.CurrentVersionNumber,
            program.CurrentVersionId,
            program.CreatedAtUtc,
            program.UpdatedAtUtc,
            currentVersionDto,
            versionInfos);
    }

    private static ProgramVersionResponse ToProgramVersionResponse(ProgramVersion version) =>
        new(
            version.Id,
            version.ProgramId,
            version.VersionNumber,
            version.Name,
            version.Description,
            version.CreatedAtUtc,
            version.Exercises
                .OrderBy(e => e.DisplayOrder)
                .Select(e => new ProgramExerciseSnapshotDto(
                    e.ExerciseId,
                    e.Exercise?.Name ?? e.ExerciseId,
                    e.Exercise?.PrimaryMuscleGroup ?? string.Empty,
                    e.Exercise?.Category ?? "Resistance",
                    e.DisplayOrder,
                    e.TargetSets,
                    e.TargetReps,
                    e.TargetLoadKg,
                    e.RestSeconds))
                .ToList());

    private static JsonHttpResult<ApiErrorResponse> ForbiddenResult() =>
        TypedResults.Json(
            new ApiErrorResponse("You do not have permission to access another user's resource."),
            statusCode: StatusCodes.Status403Forbidden);
}

public sealed record ExerciseDto(
    string Id,
    string Name,
    string PrimaryMuscleGroup,
    string Category,
    string DefaultEquipment,
    string Description);

public sealed record ProgramTemplateDto(
    string Id,
    string Name,
    string Description,
    string RecommendedExperienceLevel,
    IReadOnlyList<ProgramExerciseSnapshotDto> Exercises);

public sealed record ProgramExerciseInput(
    string ExerciseId,
    int TargetSets,
    int TargetReps,
    decimal? TargetLoadKg,
    int? RestSeconds);

public sealed record UpsertProgramRequest(
    string? Name,
    string? Description,
    string? TemplateId,
    IReadOnlyList<ProgramExerciseInput>? Exercises);

public sealed record ProgramExerciseSnapshotDto(
    string ExerciseId,
    string ExerciseName,
    string PrimaryMuscleGroup,
    string Category,
    int DisplayOrder,
    int TargetSets,
    int TargetReps,
    decimal? TargetLoadKg,
    int? RestSeconds);

public sealed record ProgramVersionHeaderDto(
    Guid VersionId,
    int VersionNumber,
    string Name,
    DateTimeOffset CreatedAtUtc);

public sealed record ProgramVersionResponse(
    Guid VersionId,
    Guid ProgramId,
    int VersionNumber,
    string Name,
    string? Description,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ProgramExerciseSnapshotDto> Exercises);

public sealed record ProgramSummaryResponse(
    Guid Id,
    string OwnerUid,
    string Name,
    string? Description,
    string? SourceTemplateId,
    int CurrentVersionNumber,
    Guid CurrentVersionId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProgramDetailResponse(
    Guid Id,
    string OwnerUid,
    string Name,
    string? Description,
    string? SourceTemplateId,
    int CurrentVersionNumber,
    Guid CurrentVersionId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    ProgramVersionResponse CurrentVersion,
    IReadOnlyList<ProgramVersionHeaderDto> Versions);
