using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GymTracker.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GymTracker.Api.Tests;

[CollectionDefinition(Name)]
public sealed class PostgresApiCollection : ICollectionFixture<PostgresApiFactory>
{
    public const string Name = "PostgresApi";
}

public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DefaultConnectionString =
        "Host=localhost;Database=gymtracker;Username=gymtracker;Password=gymtracker-ci";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__GymTracker");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = DefaultConnectionString;
            }

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GymTracker"] = connectionString,
                ["FirebaseAuth:ProjectId"] = "gymtracker-pilot",
                ["FirebaseAuth:AllowDevelopmentTokens"] = "true",
                ["Ai:UseStub"] = "true",
                ["Ai:Enabled"] = "true"
            });
        });
    }

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GymTrackerDbContext>();
        await db.Database.MigrateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public HttpClient CreateAuthenticatedClient(
        string uid,
        string? email = null,
        string signInProvider = "password")
    {
        var client = CreateClient();
        var jwt = CreateFirebaseJwt(
            uid: uid,
            email: email ?? $"{uid}@example.com",
            signInProvider: signInProvider,
            expiresAtUtc: DateTimeOffset.UtcNow.AddHours(1));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    public static string CreateFirebaseJwt(
        string uid,
        string email,
        string signInProvider = "password",
        DateTimeOffset? expiresAtUtc = null,
        string projectId = "gymtracker-pilot",
        string? issuerOverride = null)
    {
        var headerJson = JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" });
        var exp = (expiresAtUtc ?? DateTimeOffset.UtcNow.AddHours(1)).ToUnixTimeSeconds();
        var payloadJson = JsonSerializer.Serialize(new
        {
            iss = issuerOverride ?? $"https://securetoken.google.com/{projectId}",
            aud = projectId,
            user_id = uid,
            sub = uid,
            email,
            exp,
            firebase = new
            {
                sign_in_provider = signInProvider
            }
        });

        return $"{ToBase64Url(headerJson)}.{ToBase64Url(payloadJson)}.signature";
    }

    private static string ToBase64Url(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
