using GymTracker.Api.Ai;
using GymTracker.Api.Auth;
using GymTracker.Api.Data;
using GymTracker.Api.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOpenApi();
builder.Services.AddDbContext<GymTrackerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("GymTracker")));

builder.Services.Configure<FirebaseAuthOptions>(
    builder.Configuration.GetSection(FirebaseAuthOptions.SectionName));
builder.Services.AddSingleton<IFirebaseTokenVerifier, FirebaseTokenVerifier>();
builder.Services.AddAuthentication(FirebaseAuthConstants.SchemeName)
    .AddScheme<FirebaseAuthOptions, FirebaseAuthenticationHandler>(
        FirebaseAuthConstants.SchemeName,
        _ => { });
builder.Services.AddAuthorization();

builder.Services.Configure<AiProviderOptions>(
    builder.Configuration.GetSection(AiProviderOptions.SectionName));
builder.Services.AddSingleton<AiBudgetTracker>();
builder.Services.AddSingleton<StubAiProviderAdapter>();
builder.Services.AddHttpClient<GeminiAiProviderAdapter>();
builder.Services.AddScoped<IAiProviderAdapter>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AiProviderOptions>>().Value;
    return options.UseStub || string.IsNullOrWhiteSpace(options.ApiKey)
        ? sp.GetRequiredService<StubAiProviderAdapter>()
        : sp.GetRequiredService<GeminiAiProviderAdapter>();
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<GymTrackerDbContext>().Database.MigrateAsync();
}

app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")))
    .WithName("GetHealth")
    .WithSummary("Check whether the API process is running");

app.MapIdentityAndProfileEndpoints();
app.MapProgramEndpoints();
app.MapAiAdapterEndpoints();

await app.RunAsync();

public sealed record HealthResponse(string Status);

public partial class Program
{
}
