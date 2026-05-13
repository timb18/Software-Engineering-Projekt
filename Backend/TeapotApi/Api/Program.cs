using System.Text.Json;
using System.Text.Json.Serialization;
using Api;
using Api.Authorization;
using Auth0.AspNetCore.Authentication.Api;
using Auth0Net.DependencyInjection;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Add services to the container.
builder.Services.AddTeapotServices();

var jsonStringEnumConverter = new JsonStringEnumConverter(
    JsonNamingPolicy.CamelCase,
    false);
var auth0Config = builder.Configuration.GetSection("Auth0").Get<Auth0Config>();

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
        o.SupportNonNullableReferenceTypes();
        o.NonNullableReferenceTypesAsRequired();
        if (auth0Config is not null)
        {
            o.AddSecurityDefinition("Auth0", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri($"https://{auth0Config.Domain}/authorize"),
                        TokenUrl = new Uri($"https://{auth0Config.Domain}/oauth/token")
                    }
                },
                Scheme = "Auth0"
            });
        }
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

// Auth
if (auth0Config is not null)
{
    builder.Services.AddSingleton(auth0Config).AddAuth0ApiAuthentication(options =>
    {
        options.Domain = auth0Config.Domain;
        options.JwtBearerOptions = new JwtBearerOptions
        {
            Audience = auth0Config.Audience
        };
    }).Services.AddAuth0AuthenticationClient(config =>
    {
        config.Domain = auth0Config.Domain;
        config.ClientId = auth0Config.ClientId;
        config.ClientSecret = auth0Config.ClientSecret;
    }).Services.AddAuth0ManagementClient();
}
else
{
    Console.WriteLine("[DEV] Auth0 is not configured — authentication is disabled for local API startup.");
    builder.Services.AddAuthentication();
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAuthRequirement.PolicyName,
        policy => policy.Requirements.Add(new AdminAuthRequirement()));
}).AddSingleton<IAuthorizationHandler, AdminAuthHandler>();

builder.Services.AddAuthorization();

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

    return
        $"Host={uri.Host};Port={uri.Port};Database={databaseName};Username={username};Password={password};SSL Mode={sslMode};Trust Server Certificate=true";
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
    return
        $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode={sslMode};Trust Server Certificate=true";
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
            options.UseInMemoryDatabase("TeapotDev")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
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
    .AddScoped<IUserTaskRepository, UserTaskRepository>()
    .AddScoped<ITaskDependencyRepository, TaskDependencyRepository>()
    .AddScoped<ITaskBlockRepository, TaskBlockRepository>();

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection(ResendOptions.SectionName));

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();