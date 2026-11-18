using System.Security.Claims;
using GymTracker.Api.Auth;
using GymTracker.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GymTracker.Api.Endpoints;

public static class WorkoutEndpoints
{
    public static IEndpointRouteBuilder MapWorkoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workouts").RequireAuthorization();
        group.MapGet("", ListAsync).WithName("ListWorkouts")
            .WithSummary("List the authenticated user's workout records, newest first");
        group.MapPost("", CreateAsync).WithName("CreateWorkout")
            .WithSummary("Create a workout and its ordered sets atomically; loads are in kilograms");
        group.MapGet("/{workoutId:guid}", GetAsync).WithName("GetWorkout")
            .WithSummary("Get an owned workout and its sets");
        group.MapPut("/{workoutId:guid}", UpdateAsync).WithName("UpdateWorkout")
            .WithSummary("Replace an owned workout and its complete ordered set list atomically");
        group.MapDelete("/{workoutId:guid}", DeleteAsync).WithName("DeleteWorkout")
            .WithSummary("Delete an owned workout and its sets");
        return app;
    }

    private static async Task<Ok<IReadOnlyList<WorkoutResponse>>> ListAsync(
        ClaimsPrincipal principal, GymTrackerDbContext db, CancellationToken cancellationToken)
    {
        var uid = principal.GetFirebaseUid()!;
        var records = await db.WorkoutRecords.AsNoTracking().Include(w => w.Sets)
            .Where(w => w.OwnerUid == uid)
            .OrderByDescending(w => w.PerformedAtUtc).ThenBy(w => w.Id)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<WorkoutResponse>>(records.Select(ToResponse).ToList());
    }

    private static async Task<Results<Ok<WorkoutResponse>, NotFound<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>>> GetAsync(
        Guid workoutId, ClaimsPrincipal principal, GymTrackerDbContext db, CancellationToken cancellationToken)
    {
        var record = await db.WorkoutRecords.AsNoTracking().Include(w => w.Sets)
            .SingleOrDefaultAsync(w => w.Id == workoutId, cancellationToken);
        if (record is null)
        {
            return TypedResults.NotFound(new ApiErrorResponse("Workout not found."));
        }
        if (!principal.IsOwner(record.OwnerUid))
        {
            return ForbiddenResult();
        }
        return TypedResults.Ok(ToResponse(record));
    }

    private static async Task<Results<Created<WorkoutResponse>, BadRequest<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>>> CreateAsync(
        UpsertWorkoutRequest request, ClaimsPrincipal principal, GymTrackerDbContext db,
        TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var uid = principal.GetFirebaseUid()!;
        var error = await ValidateAsync(request, uid, db, cancellationToken);
        if (error is not null)
        {
            return error.Status == StatusCodes.Status403Forbidden
                ? ForbiddenResult()
                : TypedResults.BadRequest(new ApiErrorResponse(error.Message));
        }

        var now = timeProvider.GetUtcNow();
        if (!await db.UserAccounts.AnyAsync(u => u.FirebaseUid == uid, cancellationToken))
        {
            db.UserAccounts.Add(new UserAccount
            {
                FirebaseUid = uid,
                Email = principal.GetEmail(),
                AuthProvider = principal.GetSignInProvider(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
        var record = new WorkoutRecord
        {
            Id = Guid.NewGuid(),
            OwnerUid = uid,
            PerformedAtUtc = request.PerformedAtUtc.ToUniversalTime(),
            ProgramVersionId = request.ProgramVersionId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        record.Sets = request.Sets!.Select((set, index) => NewSet(record.Id, index + 1, set)).ToList();
        db.WorkoutRecords.Add(record);
        // EF wraps the account, workout and set writes in a single transaction.
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/workouts/{record.Id}", ToResponse(record));
    }

    private static async Task<Results<Ok<WorkoutResponse>, BadRequest<ApiErrorResponse>, NotFound<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>>> UpdateAsync(
        Guid workoutId, UpsertWorkoutRequest request, ClaimsPrincipal principal, GymTrackerDbContext db,
        TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var record = await db.WorkoutRecords.Include(w => w.Sets)
            .SingleOrDefaultAsync(w => w.Id == workoutId, cancellationToken);
        if (record is null)
        {
            return TypedResults.NotFound(new ApiErrorResponse("Workout not found."));
        }
        if (!principal.IsOwner(record.OwnerUid))
        {
            return ForbiddenResult();
        }
        var error = await ValidateAsync(request, record.OwnerUid, db, cancellationToken);
        if (error is not null)
        {
            return error.Status == StatusCodes.Status403Forbidden
                ? ForbiddenResult()
                : TypedResults.BadRequest(new ApiErrorResponse(error.Message));
        }

        record.PerformedAtUtc = request.PerformedAtUtc.ToUniversalTime();
        record.ProgramVersionId = request.ProgramVersionId;
        record.UpdatedAtUtc = timeProvider.GetUtcNow();
        var oldSets = record.Sets.OrderBy(s => s.DisplayOrder).ToList();
        for (var index = 0; index < request.Sets!.Count; index++)
        {
            var input = request.Sets[index];
            if (index < oldSets.Count)
            {
                oldSets[index].ExerciseId = input.ExerciseId.Trim();
                oldSets[index].Repetitions = input.Repetitions;
                oldSets[index].LoadKg = input.LoadKg;
            }
            else
            {
                var added = NewSet(record.Id, index + 1, input);
                record.Sets.Add(added);
                db.WorkoutSets.Add(added);
            }
        }
        foreach (var removed in oldSets.Skip(request.Sets.Count))
        {
            record.Sets.Remove(removed);
            db.WorkoutSets.Remove(removed);
        }
        // Validate before mutating, then commit the record and all set changes together.
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(record));
    }

    private static async Task<Results<NoContent, NotFound<ApiErrorResponse>, JsonHttpResult<ApiErrorResponse>>> DeleteAsync(
        Guid workoutId, ClaimsPrincipal principal, GymTrackerDbContext db, CancellationToken cancellationToken)
    {
        var record = await db.WorkoutRecords.SingleOrDefaultAsync(w => w.Id == workoutId, cancellationToken);
        if (record is null)
        {
            return TypedResults.NotFound(new ApiErrorResponse("Workout not found."));
        }
        if (!principal.IsOwner(record.OwnerUid))
        {
            return ForbiddenResult();
        }
        db.WorkoutRecords.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<ValidationError?> ValidateAsync(
        UpsertWorkoutRequest request, string uid, GymTrackerDbContext db, CancellationToken cancellationToken)
    {
        if (request.PerformedAtUtc == default)
        {
            return new("Workout time is required.");
        }
        if (request.Sets is null || request.Sets.Count == 0 || request.Sets.Count > 1500)
        {
            return new("A workout must contain between 1 and 1500 sets.");
        }
        if (request.Sets.Any(s => s is null || string.IsNullOrWhiteSpace(s.ExerciseId)
            || s.Repetitions is <= 0 or > 500 || s.LoadKg is < 0 or > 1000
            || (s.LoadKg.HasValue && decimal.Round(s.LoadKg.Value, 2) != s.LoadKg.Value)))
        {
            return new("Each set requires a catalogue exercise, 1–500 repetitions and an optional load of 0–1000 kg with at most two decimal places.");
        }
        var ids = request.Sets.Select(s => s.ExerciseId.Trim()).Distinct().ToList();
        if (await db.Exercises.CountAsync(e => ids.Contains(e.Id), cancellationToken) != ids.Count)
        {
            return new("A referenced exercise is not in the reviewed catalogue.");
        }
        if (request.ProgramVersionId is Guid versionId)
        {
            var version = await db.ProgramVersions.AsNoTracking()
                .Where(v => v.Id == versionId).Select(v => new { v.Program!.OwnerUid })
                .SingleOrDefaultAsync(cancellationToken);
            if (version is null)
            {
                return new("Program version not found.");
            }
            if (version.OwnerUid != uid)
            {
                return new("You do not have permission to access another user's resource.", StatusCodes.Status403Forbidden);
            }
        }
        return null;
    }

    private static WorkoutSet NewSet(Guid workoutId, int order, WorkoutSetInput input) => new()
    {
        Id = Guid.NewGuid(),
        WorkoutId = workoutId,
        DisplayOrder = order,
        ExerciseId = input.ExerciseId.Trim(),
        Repetitions = input.Repetitions,
        LoadKg = input.LoadKg
    };

    private static WorkoutResponse ToResponse(WorkoutRecord record) => new(
        record.Id, record.PerformedAtUtc, record.ProgramVersionId,
        record.Sets.OrderBy(s => s.DisplayOrder)
            .Select(s => new WorkoutSetInput(s.ExerciseId, s.Repetitions, s.LoadKg)).ToList());

    private static JsonHttpResult<ApiErrorResponse> ForbiddenResult() => TypedResults.Json(
        new ApiErrorResponse("You do not have permission to access another user's resource."),
        statusCode: StatusCodes.Status403Forbidden);

    private sealed record ValidationError(string Message, int Status = StatusCodes.Status400BadRequest);
}

public sealed record WorkoutSetInput(string ExerciseId, int Repetitions, decimal? LoadKg);
public sealed record UpsertWorkoutRequest(
    DateTimeOffset PerformedAtUtc, Guid? ProgramVersionId, IReadOnlyList<WorkoutSetInput>? Sets);
public sealed record WorkoutResponse(
    Guid Id, DateTimeOffset PerformedAtUtc, Guid? ProgramVersionId, IReadOnlyList<WorkoutSetInput> Sets);
