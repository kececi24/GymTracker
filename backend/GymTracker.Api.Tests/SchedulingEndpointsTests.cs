using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using GymTracker.Api.Auth;
using GymTracker.Api.Endpoints;
using Xunit;

namespace GymTracker.Api.Tests;

[Collection(PostgresApiCollection.Name)]
public sealed class SchedulingEndpointsTests(PostgresApiFactory factory)
{
    // WBS-4.2 contract: POST creates, PUT moves, and DELETE marks the session cancelled.
    // Write requests use a local wall-clock time without an offset. The owner's profile
    // supplies the display timezone. Responses expose both the UTC instant and local time.
    private const string SessionsPath = "/api/scheduled-sessions";
    private const string ValidLocalStart = "2028-03-27T18:30:00";

    [Theory]
    [InlineData("POST", SessionsPath)]
    [InlineData("PUT", SessionsPath + "/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", SessionsPath + "/00000000-0000-0000-0000-000000000001")]
    public async Task SchedulingRoutes_RequireAuthentication(string method, string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method != "DELETE")
        {
            request.Content = JsonContent.Create(new { localStart = ValidLocalStart });
        }

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("UTC", "2028-03-27T18:30:00", "2028-03-27T18:30:00Z")]
    [InlineData("Europe/Istanbul", "2028-03-27T00:30:00", "2028-03-26T21:30:00Z")]
    [InlineData("Europe/Berlin", "2028-03-25T18:30:00", "2028-03-25T17:30:00Z")]
    [InlineData("Europe/Berlin", "2028-03-27T18:30:00", "2028-03-27T16:30:00Z")]
    public async Task Create_ConvertsLocalStartToUtcUsingOwnersDisplayTimezone(
        string timezone, string localStart, string expectedUtc)
    {
        using var client = await CreateClientWithTimezoneAsync(timezone);

        var session = await CreateSessionAsync(client, localStart);

        AssertStart(session, timezone, localStart, expectedUtc);
        Assert.Null(session.CancelledAtUtc);
    }

    [Fact]
    public async Task Move_RecomputesUtcAcrossDaylightSaving_AndCancelReturnsSavedSession()
    {
        using var client = await CreateClientWithTimezoneAsync("Europe/Berlin");
        var created = await CreateSessionAsync(client, "2028-03-25T18:30:00");
        AssertStart(created, "Europe/Berlin", "2028-03-25T18:30:00", "2028-03-25T17:30:00Z");
        var path = $"{SessionsPath}/{created.Id}";

        using var move = await client.PutAsJsonAsync(path, new { localStart = ValidLocalStart });
        var moved = await ReadSessionAsync(move, HttpStatusCode.OK);
        Assert.Equal(created.Id, moved.Id);
        AssertStart(moved, "Europe/Berlin", ValidLocalStart, "2028-03-27T16:30:00Z");
        Assert.Null(moved.CancelledAtUtc);

        using var cancel = await client.DeleteAsync(path);
        var cancelled = await ReadSessionAsync(cancel, HttpStatusCode.OK);
        Assert.Equal(moved.Id, cancelled.Id);
        AssertStart(cancelled, "Europe/Berlin", ValidLocalStart, "2028-03-27T16:30:00Z");
        Assert.NotNull(cancelled.CancelledAtUtc);
        Assert.Equal(TimeSpan.Zero, cancelled.CancelledAtUtc.Value.Offset);

        using var moveCancelled = await client.PutAsJsonAsync(path, new { localStart = "2028-03-28T18:30:00" });
        Assert.Equal(HttpStatusCode.Conflict, moveCancelled.StatusCode);

        using var cancelAgain = await client.DeleteAsync(path);
        var stillCancelled = await ReadSessionAsync(cancelAgain, HttpStatusCode.OK);
        Assert.Equal(cancelled, stillCancelled);
    }

    [Theory]
    [InlineData("2028-03-26T02:30:00")] // Daylight-saving gap in Berlin.
    [InlineData("2028-10-29T02:30:00")] // Daylight-saving overlap in Berlin.
    [InlineData("2028-02-30T18:30:00")]
    [InlineData("2028-03-27T18:30:00+02:00")]
    [InlineData("")]
    public async Task InvalidLocalStart_IsRejectedOnCreateAndMove_AndKeepsOriginalStart(string localStart)
    {
        using var client = await CreateClientWithTimezoneAsync("Europe/Berlin");
        var created = await CreateSessionAsync(client, ValidLocalStart);
        var path = $"{SessionsPath}/{created.Id}";

        using var invalidCreate = await client.PostAsJsonAsync(SessionsPath, new { localStart });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        using var invalidMove = await client.PutAsJsonAsync(path, new { localStart });
        Assert.Equal(HttpStatusCode.BadRequest, invalidMove.StatusCode);

        using var cancel = await client.DeleteAsync(path);
        var cancelled = await ReadSessionAsync(cancel, HttpStatusCode.OK);
        AssertStart(cancelled, "Europe/Berlin", ValidLocalStart, "2028-03-27T16:30:00Z");
    }

    [Fact]
    public async Task Create_RejectsUnrecognizedDisplayTimezone()
    {
        // Profile updates currently store the supplied timezone string as-is after trimming.
        using var client = await CreateClientWithTimezoneAsync("Invalid/Timezone");

        using var response = await client.PostAsJsonAsync(SessionsPath, new { localStart = ValidLocalStart });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NonOwnerCannotMoveOrCancelSession()
    {
        using var owner = await CreateClientWithTimezoneAsync("Europe/Istanbul");
        using var other = await CreateClientWithTimezoneAsync("UTC");
        var created = await CreateSessionAsync(owner, ValidLocalStart);
        var path = $"{SessionsPath}/{created.Id}";

        using var move = await other.PutAsJsonAsync(path, new { localStart = "2028-03-28T18:30:00" });
        Assert.Equal(HttpStatusCode.Forbidden, move.StatusCode);
        using var cancel = await other.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);

        using var ownerCancel = await owner.DeleteAsync(path);
        var cancelled = await ReadSessionAsync(ownerCancel, HttpStatusCode.OK);
        Assert.Equal(created.Id, cancelled.Id);
        AssertStart(cancelled, "Europe/Istanbul", ValidLocalStart, "2028-03-27T15:30:00Z");
    }

    [Fact]
    public async Task MissingSession_ReturnsNotFoundOnMoveAndCancel()
    {
        using var client = await CreateClientWithTimezoneAsync("UTC");
        var path = $"{SessionsPath}/{Guid.NewGuid()}";

        using var move = await client.PutAsJsonAsync(path, new { localStart = ValidLocalStart });
        await AssertSessionNotFoundAsync(move);
        using var cancel = await client.DeleteAsync(path);
        await AssertSessionNotFoundAsync(cancel);
    }

    private async Task<HttpClient> CreateClientWithTimezoneAsync(string timezone)
    {
        var uid = $"schedule-{Guid.NewGuid():N}";
        var client = factory.CreateAuthenticatedClient(uid);
        try
        {
            using var response = await client.PutAsJsonAsync($"/api/users/{uid}/profile",
                new UpdateProfileRequest("Schedule Tester", null, null, null, null, null, null, timezone));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task<SessionResponse> CreateSessionAsync(HttpClient client, string localStart)
    {
        using var response = await client.PostAsJsonAsync(SessionsPath, new { localStart });
        var session = await ReadSessionAsync(response, HttpStatusCode.Created);
        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal($"{SessionsPath}/{session.Id}", response.Headers.Location?.OriginalString);
        return session;
    }

    private static async Task<SessionResponse> ReadSessionAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        return session;
    }

    private static async Task AssertSessionNotFoundAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.False(string.IsNullOrWhiteSpace(error?.Error));
    }

    private static void AssertStart(SessionResponse session, string timezone, string localStart, string utcStart)
    {
        Assert.Equal(timezone, session.DisplayTimezone);
        Assert.Equal(DateTime.ParseExact(localStart, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture), session.LocalStart);
        Assert.Equal(DateTimeKind.Unspecified, session.LocalStart.Kind);
        Assert.Equal(DateTimeOffset.Parse(utcStart, CultureInfo.InvariantCulture), session.StartAtUtc);
        Assert.Equal(TimeSpan.Zero, session.StartAtUtc.Offset);
    }

    private sealed record SessionResponse(
        Guid Id, DateTime LocalStart, DateTimeOffset StartAtUtc, string DisplayTimezone, DateTimeOffset? CancelledAtUtc);
}
