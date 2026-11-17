using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GymTracker.Api.Tests;

public sealed class FoundationTests(WebApplicationFactory<Program> app)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task HealthReturnsOk()
    {
        using var client = app.CreateClient();

        var health = await client.GetFromJsonAsync<HealthResponse>("/health");

        Assert.Equal("ok", health?.Status);
    }

    [Fact]
    public async Task OpenApiDescribesHealthEndpoint()
    {
        using var client = app.CreateClient();
        using var document = await client.GetFromJsonAsync<JsonDocument>("/openapi/v1.json");

        Assert.NotNull(document);
        Assert.True(document.RootElement.GetProperty("paths").GetProperty("/health").TryGetProperty("get", out _));
    }
}
