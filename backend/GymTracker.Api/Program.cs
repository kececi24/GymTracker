using GymTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<GymTrackerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("GymTracker")));

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<GymTrackerDbContext>().Database.MigrateAsync();
}

app.MapOpenApi();

app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")))
    .WithName("GetHealth")
    .WithSummary("Check whether the API process is running");

await app.RunAsync();

public sealed record HealthResponse(string Status);

public partial class Program
{
}
