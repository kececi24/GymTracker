using System.Globalization;
using System.Security.Claims;
using GymTracker.Api.Auth;
using GymTracker.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GymTracker.Api.Endpoints;

public static class SchedulingEndpoints
{
    private const string SessionsPath = "/api/scheduled-sessions";
    private static readonly string[] LocalStartFormats =
        ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFF"];

    public static IEndpointRouteBuilder MapSchedulingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(SessionsPath).RequireAuthorization();

        group.MapPost("", CreateAsync)
            .WithName("CreateScheduledSession")
            .WithSummary("Schedule a session using the caller's profile timezone")
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized);
        group.MapPut("/{sessionId:guid}", MoveAsync)
            .WithName("MoveScheduledSession")
            .WithSummary("Move an owned active session using the caller's current profile timezone")
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden);
        group.MapDelete("/{sessionId:guid}", CancelAsync)
            .WithName("CancelScheduledSession")
            .WithSummary("Mark an owned session cancelled and return its saved state")
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<Results<Created<ScheduledSessionResponse>, BadRequest<ApiErrorResponse>>> CreateAsync(
        ScheduleSessionRequest request, ClaimsPrincipal principal, GymTrackerDbContext db,
        TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var uid = principal.GetFirebaseUid()!;
        var profileTimezone = await FindProfileTimezoneAsync(db, uid, cancellationToken);
        var displayTimezone = profileTimezone ?? "UTC";
        var (startAtUtc, error) = ResolveStart(request.LocalStart, displayTimezone);
        if (error is not null)
        {
            return TypedResults.BadRequest(error);
        }

        if (profileTimezone is null)
        {
            var now = timeProvider.GetUtcNow();
            db.UserAccounts.Add(new UserAccount
            {
                FirebaseUid = uid,
                Email = principal.GetEmail(),
                AuthProvider = principal.GetSignInProvider(),
                DisplayTimezone = displayTimezone,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        var session = new ScheduledSession
        {
            Id = Guid.NewGuid(),
            OwnerUid = uid,
            StartAtUtc = startAtUtc,
            DisplayTimezone = displayTimezone
        };
        db.ScheduledSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"{SessionsPath}/{session.Id}", ToResponse(session));
    }

    private static async Task<Results<Ok<ScheduledSessionResponse>, BadRequest<ApiErrorResponse>,
        NotFound<ApiErrorResponse>, Conflict<ApiErrorResponse>, ForbidHttpResult>> MoveAsync(
        Guid sessionId, ScheduleSessionRequest request, ClaimsPrincipal principal,
        GymTrackerDbContext db, CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(db, sessionId, cancellationToken);
        if (session is null)
        {
            return SessionNotFound();
        }
        if (!principal.IsOwner(session.OwnerUid))
        {
            return TypedResults.Forbid();
        }
        if (session.CancelledAtUtc is not null)
        {
            return CancelledSessionConflict();
        }

        var displayTimezone = await FindProfileTimezoneAsync(db, session.OwnerUid, cancellationToken);
        if (displayTimezone is null)
        {
            return SessionNotFound();
        }
        var (startAtUtc, error) = ResolveStart(request.LocalStart, displayTimezone);
        if (error is not null)
        {
            return TypedResults.BadRequest(error);
        }

        var moved = await ActiveSession(db, session.Id, session.OwnerUid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.StartAtUtc, startAtUtc)
                .SetProperty(x => x.DisplayTimezone, displayTimezone), cancellationToken);
        if (moved == 0)
        {
            // The conditional update prevents a move from overwriting a concurrent cancellation.
            return await FindSessionAsync(db, sessionId, cancellationToken) is null
                ? SessionNotFound()
                : CancelledSessionConflict();
        }

        session.StartAtUtc = startAtUtc;
        session.DisplayTimezone = displayTimezone;
        return TypedResults.Ok(ToResponse(session));
    }

    private static async Task<Results<Ok<ScheduledSessionResponse>, NotFound<ApiErrorResponse>, ForbidHttpResult>> CancelAsync(
        Guid sessionId, ClaimsPrincipal principal, GymTrackerDbContext db,
        TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var uid = principal.GetFirebaseUid()!;
        var cancelledAtUtc = timeProvider.GetUtcNow();
        await ActiveSession(db, sessionId, uid)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CancelledAtUtc, cancelledAtUtc), cancellationToken);

        // Reload the saved state, including the original timestamp on repeat cancellation.
        var session = await FindSessionAsync(db, sessionId, cancellationToken);
        if (session is null)
        {
            return SessionNotFound();
        }
        if (!principal.IsOwner(session.OwnerUid))
        {
            return TypedResults.Forbid();
        }
        return TypedResults.Ok(ToResponse(session));
    }

    private static Task<string?> FindProfileTimezoneAsync(GymTrackerDbContext db, string uid, CancellationToken cancellationToken) =>
        db.UserAccounts.Where(u => u.FirebaseUid == uid).Select(u => u.DisplayTimezone)
            .SingleOrDefaultAsync(cancellationToken);

    private static Task<ScheduledSession?> FindSessionAsync(GymTrackerDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.ScheduledSessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    private static IQueryable<ScheduledSession> ActiveSession(GymTrackerDbContext db, Guid id, string ownerUid) =>
        db.ScheduledSessions.Where(s => s.Id == id && s.OwnerUid == ownerUid && s.CancelledAtUtc == null);

    private static (DateTimeOffset StartAtUtc, ApiErrorResponse? Error) ResolveStart(string? input, string timezone)
    {
        if (!DateTime.TryParseExact(input, LocalStartFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var localStart))
        {
            return (default, new ApiErrorResponse("Local start must be an ISO date and time without a UTC offset."));
        }
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out var zone))
        {
            return (default, new ApiErrorResponse("The profile display timezone is not recognized."));
        }
        if (zone.IsInvalidTime(localStart))
        {
            return (default, new ApiErrorResponse("Local start does not exist in the display timezone due to a clock change."));
        }
        if (zone.IsAmbiguousTime(localStart))
        {
            return (default, new ApiErrorResponse("Local start occurs twice in the display timezone due to a clock change."));
        }

        var utc = TimeZoneInfo.ConvertTimeToUtc(localStart, zone);
        if (utc == DateTime.MinValue || utc == DateTime.MaxValue)
        {
            return (default, new ApiErrorResponse("Local start is outside the supported UTC range."));
        }
        return (new DateTimeOffset(utc), null);
    }

    private static ScheduledSessionResponse ToResponse(ScheduledSession session)
    {
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(session.DisplayTimezone);
        var local = TimeZoneInfo.ConvertTime(session.StartAtUtc, timezone).DateTime;
        return new ScheduledSessionResponse(session.Id, local, session.StartAtUtc, session.DisplayTimezone, session.CancelledAtUtc);
    }

    private static NotFound<ApiErrorResponse> SessionNotFound() =>
        TypedResults.NotFound(new ApiErrorResponse("Scheduled session not found."));

    private static Conflict<ApiErrorResponse> CancelledSessionConflict() =>
        TypedResults.Conflict(new ApiErrorResponse("A cancelled session cannot be moved."));
}

public sealed record ScheduleSessionRequest(string? LocalStart);
public sealed record ScheduledSessionResponse(
    Guid Id, DateTime LocalStart, DateTimeOffset StartAtUtc, string DisplayTimezone, DateTimeOffset? CancelledAtUtc);
