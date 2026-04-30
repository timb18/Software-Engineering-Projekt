using System.Text.Json;
using System.Text.Json.Serialization;
using Api;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddTeapotServices();

var jsonStringEnumConverter = new JsonStringEnumConverter(
    JsonNamingPolicy.CamelCase,
    false);

// Swagger
builder.Services.AddEndpointsApiExplorer()
    .ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(jsonStringEnumConverter);
        options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    })
    .Configure<JsonOptions>(options =>
    {
        options.JsonSerializerOptions.Converters.Add(jsonStringEnumConverter);
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    })
    .AddSwaggerGen(o =>
    {
        o.SwaggerDoc("v1",
            new OpenApiInfo
                { Title = "OfficeDashboardApi", Version = "v1", Description = "Backend API for the Office Dashboard" });
    })
    .AddCors(options => options.AddDefaultPolicy(c => { c.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader(); }));

// Data Access
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Railway provides DATABASE_URL as a fallback
if (string.IsNullOrWhiteSpace(connectionString))
{
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        connectionString = TryBuildConnectionStringFromDatabaseUrl(databaseUrl);
    }
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = TryBuildConnectionStringFromDiscreteEnvironmentVariables();
}

var useInMemory = string.IsNullOrWhiteSpace(connectionString);
if (useInMemory)
    Console.WriteLine("[DEV] No connection string found — using in-memory database.");

static string? TryBuildConnectionStringFromDatabaseUrl(string databaseUrl)
{
    var normalizedUrl = databaseUrl.StartsWith("jdbc:", StringComparison.OrdinalIgnoreCase)
        ? databaseUrl["jdbc:".Length..]
        : databaseUrl;

    if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
    {
        return null;
    }

    var queryParams = ParseQueryString(uri.Query);
    var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.TrimEntries);

    var username = userInfo.Length > 0 && !string.IsNullOrWhiteSpace(userInfo[0])
        ? userInfo[0]
        : GetFirstNonEmpty(
            queryParams.GetValueOrDefault("user"),
            queryParams.GetValueOrDefault("username"),
            Environment.GetEnvironmentVariable("PGUSER"),
            Environment.GetEnvironmentVariable("POSTGRES_USER"));

    var password = userInfo.Length > 1 && !string.IsNullOrWhiteSpace(userInfo[1])
        ? userInfo[1]
        : GetFirstNonEmpty(
            queryParams.GetValueOrDefault("password"),
            Environment.GetEnvironmentVariable("PGPASSWORD"),
            Environment.GetEnvironmentVariable("POSTGRES_PASSWORD"));

    var databaseName = string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/'))
        ? GetFirstNonEmpty(
            queryParams.GetValueOrDefault("database"),
            Environment.GetEnvironmentVariable("PGDATABASE"),
            Environment.GetEnvironmentVariable("POSTGRES_DB"))
        : uri.AbsolutePath.Trim('/');

    if (string.IsNullOrWhiteSpace(uri.Host) ||
        string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(password) ||
        string.IsNullOrWhiteSpace(databaseName))
    {
        return null;
    }

    var sslMode = GetFirstNonEmpty(
        queryParams.GetValueOrDefault("sslmode"),
        Environment.GetEnvironmentVariable("PGSSLMODE"),
        "Require");

    return $"Host={uri.Host};Port={uri.Port};Database={databaseName};Username={username};Password={password};SSL Mode={sslMode};Trust Server Certificate=true";
}

static string? TryBuildConnectionStringFromDiscreteEnvironmentVariables()
{
    var host = GetFirstNonEmpty(
        Environment.GetEnvironmentVariable("PGHOST"),
        Environment.GetEnvironmentVariable("POSTGRES_HOST"));
    var port = GetFirstNonEmpty(
        Environment.GetEnvironmentVariable("PGPORT"),
        Environment.GetEnvironmentVariable("POSTGRES_PORT"),
        "5432");
    var database = GetFirstNonEmpty(
        Environment.GetEnvironmentVariable("PGDATABASE"),
        Environment.GetEnvironmentVariable("POSTGRES_DB"));
    var username = GetFirstNonEmpty(
        Environment.GetEnvironmentVariable("PGUSER"),
        Environment.GetEnvironmentVariable("POSTGRES_USER"));
    var password = GetFirstNonEmpty(
        Environment.GetEnvironmentVariable("PGPASSWORD"),
        Environment.GetEnvironmentVariable("POSTGRES_PASSWORD"));

    if (string.IsNullOrWhiteSpace(host) ||
        string.IsNullOrWhiteSpace(database) ||
        string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(password))
    {
        return null;
    }

    var sslMode = GetFirstNonEmpty(Environment.GetEnvironmentVariable("PGSSLMODE"), "Require");
    return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode={sslMode};Trust Server Certificate=true";
}

static string? GetFirstNonEmpty(params string?[] values) =>
    values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

static Dictionary<string, string> ParseQueryString(string query)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(query))
    {
        return result;
    }

    foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = pair.Split('=', 2);
        var key = Uri.UnescapeDataString(parts[0]);
        var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        result[key] = value;
    }

    return result;
}

builder.Services.AddDbContext<TeapotDbContext>(options =>
{
    if (useInMemory)
        options.UseInMemoryDatabase("TeapotDev");
    else
        options.UseNpgsql(connectionString, o => o
            .MapEnum<EInvitationStatus>("invitation_status")
            .MapEnum<ERole>("role")
            .MapEnum<ETaskPriority>("task_priority")
            .MapEnum<ETaskIntensity>("task_intensity"));
})
    .AddScoped<IUserRepository, UserRepository>()
    .AddScoped<IOrganizationRepository, OrganizationRepository>()
    .AddScoped<IMembershipRepository, MembershipRepository>()
    .AddScoped<IInvitationRepository, InvitationRepository>()
    .AddScoped<IWorkProfileRepository, WorkProfileRepository>()
    .AddScoped<IUserTaskRepository, UserTaskRepository>();

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));

// Services
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();
builder.Services.AddScoped<IWorkProfileService, WorkProfileService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(jsonStringEnumConverter);
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

var app = builder.Build();

await SchemaUpgradeService.ApplyAsync(app.Services);

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    o.RoutePrefix = string.Empty;
});

app.MapGet("/health", () => Results.Ok("healthy"));

app.UseCors();
// HTTPS redirect is handled by the hosting platform's load balancer; only enable locally.
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.MapControllers();

app.Run();
