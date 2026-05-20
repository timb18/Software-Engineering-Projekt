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
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.OpenApi;

/// <summary>
/// Teapot API application startup and configuration.
/// Handles service registration, database setup, authentication/authorization configuration,
/// and middleware pipeline setup for the ASP.NET Core 10 REST API.
/// </summary>

var builder = WebApplication.CreateBuilder(args);

// Configure port from environment variable (supports Railway deployment)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port)) builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Register all business logic services from the Services layer (dependency injection)
builder.Services.AddTeapotServices();

// Configure JSON serialization options to use camelCase for enum values
var jsonStringEnumConverter = new JsonStringEnumConverter(
    JsonNamingPolicy.CamelCase,
    false);
var auth0Config = builder.Configuration.GetSection("Auth0").Get<Auth0Config>();

/// Configure Swagger/OpenAPI documentation and API explorers
/// Includes OAuth2 security scheme if Auth0 is configured
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
        // Define the API documentation with version and description
        o.SwaggerDoc("v1",
            new OpenApiInfo
                { Title = "OfficeDashboardApi", Version = "v1", Description = "Backend API for the Office Dashboard" });

        // Add OAuth2/Auth0 security scheme if Auth0 is available
        if (auth0Config is not null)
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
    })
    // Configure CORS to allow requests from any origin with any method and headers
    .AddCors(options => options.AddDefaultPolicy(c => { c.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader(); }));

/// Configure database connectivity with multiple fallback strategies
/// 1. First: Try to use ConnectionString from appsettings configuration
/// 2. Second: Parse DATABASE_URL environment variable (Railway deployment)
/// 3. Third: Build from discrete POSTGRES_* or PGHOST/* environment variables
/// 4. Last: Use in-memory database for development if no connection available
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Attempt to parse Railway DATABASE_URL environment variable if configuration is empty
if (string.IsNullOrWhiteSpace(connectionString))
{
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(databaseUrl))
        connectionString = TryBuildConnectionStringFromDatabaseUrl(databaseUrl);
}

/// Configure authentication and authorization
/// Uses Auth0 JWT bearer tokens if Auth0 config is provided,
/// otherwise runs in development mode with authentication disabled
if (auth0Config is not null)
{
    // Register Auth0 configuration, API authentication, management client
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
    // Development mode: no Auth0 configured, authentication disabled
    Console.WriteLine("[DEV] Auth0 is not configured — authentication is disabled for local API startup.");
    builder.Services.AddAuthentication();
}

// Register authorization policies and handlers for admin permission checks
builder.Services.AddAuthorization(options =>
{
    // AdminAuthPolicy requires the "write:orgs" permission from Auth0 token
    options.AddPolicy(AdminAuthRequirement.PolicyName,
        policy => policy.Requirements.Add(new AdminAuthRequirement()));
}).AddSingleton<IAuthorizationHandler, AdminAuthHandler>();

// Enable authorization policy checking
builder.Services.AddAuthorization();

// Final fallback: try to build connection string from discrete PostgreSQL environment variables
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = TryBuildConnectionStringFromDiscreteEnvironmentVariables();

// If no connection string available, use in-memory database for development/testing
var useInMemory = string.IsNullOrWhiteSpace(connectionString);
if (useInMemory)
    Console.WriteLine("[DEV] No connection string found — using in-memory database.");

/// Parse Railway DATABASE_URL format (jdbc:postgresql://user:pass@host:port/db) into EF Core connection string.
/// Supports various fallback environment variables (PGHOST, PGUSER, PGPASSWORD, etc.)
static string? TryBuildConnectionStringFromDatabaseUrl(string databaseUrl)
{
    // Remove "jdbc:" prefix if present (common in Railway DATABASE_URL format)
    var normalizedUrl = databaseUrl.StartsWith("jdbc:", StringComparison.OrdinalIgnoreCase)
        ? databaseUrl["jdbc:".Length..]
        : databaseUrl;

    // Parse URL components
    if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri)) return null;

    // Extract query parameters and user credentials from URL
    var queryParams = ParseQueryString(uri.Query);
    var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.TrimEntries);

    // Resolve username from URL or fallback to environment variables
    var username = userInfo.Length > 0 && !string.IsNullOrWhiteSpace(userInfo[0])
        ? userInfo[0]
        : GetFirstNonEmpty(
            queryParams.GetValueOrDefault("user"),
            queryParams.GetValueOrDefault("username"),
            Environment.GetEnvironmentVariable("PGUSER"),
            Environment.GetEnvironmentVariable("POSTGRES_USER"));

    // Resolve password from URL or fallback to environment variables
    var password = userInfo.Length > 1 && !string.IsNullOrWhiteSpace(userInfo[1])
        ? userInfo[1]
        : GetFirstNonEmpty(
            queryParams.GetValueOrDefault("password"),
            Environment.GetEnvironmentVariable("PGPASSWORD"),
            Environment.GetEnvironmentVariable("POSTGRES_PASSWORD"));

    // Resolve database name from URL path or fallback to environment variables
    var databaseName = string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/'))
        ? GetFirstNonEmpty(
            queryParams.GetValueOrDefault("database"),
            Environment.GetEnvironmentVariable("PGDATABASE"),
            Environment.GetEnvironmentVariable("POSTGRES_DB"))
        : uri.AbsolutePath.Trim('/');

    // Validate all required connection components are available
    if (string.IsNullOrWhiteSpace(uri.Host) ||
        string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(password) ||
        string.IsNullOrWhiteSpace(databaseName))
        return null;

    // Determine SSL mode (default to Require for production)
    var sslMode = GetFirstNonEmpty(
        queryParams.GetValueOrDefault("sslmode"),
        Environment.GetEnvironmentVariable("PGSSLMODE"),
        "Require");

    // Build Entity Framework Core connection string for PostgreSQL
    return
        $"Host={uri.Host};Port={uri.Port};Database={databaseName};Username={username};Password={password};SSL Mode={sslMode};Trust Server Certificate=true";
}

/// Build connection string from discrete PostgreSQL environment variables (PGHOST, PGUSER, etc.)
/// Uses standard PostgreSQL environment variable names as fallbacks
static string? TryBuildConnectionStringFromDiscreteEnvironmentVariables()
{
    // Resolve each connection component from environment variables
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

    // Validate all required components are available
    if (string.IsNullOrWhiteSpace(host) ||
        string.IsNullOrWhiteSpace(database) ||
        string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(password))
        return null;

    // Build connection string with SSL mode
    var sslMode = GetFirstNonEmpty(Environment.GetEnvironmentVariable("PGSSLMODE"), "Require");
    return
        $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode={sslMode};Trust Server Certificate=true";
}

/// Return the first non-empty/non-null string from a list of candidates.
/// Used for resolving configuration values with multiple fallback sources.
static string? GetFirstNonEmpty(params string?[] values)
{
    return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

/// Parse URL query string into dictionary, handling URL decoding and empty values.
static Dictionary<string, string> ParseQueryString(string query)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(query)) return result;

    // Split by '&' to get key=value pairs, then parse each
    foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = pair.Split('=', 2);
        var key = Uri.UnescapeDataString(parts[0]);
        var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        result[key] = value;
    }

    return result;
}

/// Register database context and all repository implementations.
/// Configures either in-memory database (development) or PostgreSQL with enum type mappings.
builder.Services.AddDbContext<TeapotDbContext>(options =>
    {
        if (useInMemory)
            // Use in-memory database for development/testing, ignore transaction warnings
            options.UseInMemoryDatabase("TeapotDev")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        else
            // Configure PostgreSQL with custom enum type mappings
            options.UseNpgsql(connectionString, o => o
                .MapEnum<EInvitationStatus>("invitation_status")
                .MapEnum<ERole>("role")
                .MapEnum<ETaskPriority>("task_priority")
                .MapEnum<ETaskIntensity>("task_intensity"));
    })
    // Register repository implementations with scoped lifetime
    .AddScoped<IUserRepository, UserRepository>()
    .AddScoped<IOrganizationRepository, OrganizationRepository>()
    .AddScoped<IMembershipRepository, MembershipRepository>()
    .AddScoped<IInvitationRepository, InvitationRepository>()
    .AddScoped<IWorkProfileRepository, WorkProfileRepository>()
    .AddScoped<IUserTaskRepository, UserTaskRepository>()
    .AddScoped<ITaskDependencyRepository, TaskDependencyRepository>()
    .AddScoped<ITaskBlockRepository, TaskBlockRepository>();

// Configure email service options from appsettings (SMTP credentials, provider URLs, etc.)
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

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedHost |
                       ForwardedHeaders.XForwardedProto
});

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