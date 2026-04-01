using System.Text.Json.Serialization;
using PrototypeApi.Planning;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicyName = "RenderCorsPolicy";

// Render injects allowed origins via environment variable; local development falls back to open CORS.
var allowedOrigins = builder.Configuration["CORS_ALLOWED_ORIGINS"]
    ?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Enums are serialized as strings so planning payloads stay readable in demo tools and browser fetch calls.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Planning services are registered behind an interface so the algorithm can be swapped later.
builder.Services.AddSingleton<ISchedulingAlgorithm, HeuristicSchedulingAlgorithm>();
builder.Services.AddSingleton<PlanningService>();
builder.Services.AddOpenApi();

var app = builder.Build();

var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(renderPort))
{
    app.Urls.Add($"http://0.0.0.0:{renderPort}");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(CorsPolicyName);

// Group all planning endpoints under one route prefix so they are easy to find and demo.
var planningGroup = app.MapGroup("/api/planning");

planningGroup.MapGet("/default-work-profile", () => Results.Ok(WorkProfile.CreateDefault()));
planningGroup.MapGet("/default-options", () => Results.Ok(new SchedulingOptions()));
planningGroup.MapPost("/schedule", (PlanningRequest request, PlanningService planningService) =>
{
    try
    {
        // The POST endpoint is the main entry point for generating a full plan from raw task data.
        return Results.Ok(planningService.GeneratePlan(request));
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = [exception.Message]
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

// Minimal operational endpoints keep hosting and demo verification simple.
app.MapGet("/", () => Results.Ok("Prototype API is running"));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();