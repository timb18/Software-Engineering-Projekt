using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Authorization;
using Auth0.AspNetCore.Authentication.Api;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Services;

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
        o.AddSecurityDefinition("Auth0", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri($"https://{builder.Configuration["Auth0:Domain"]}/authorize"),
                    TokenUrl = new Uri($"https://{builder.Configuration["Auth0:Audience"]}/oauth/token")
                }
            },
            Scheme = "Auth0"
        });
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
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');
        connectionString =
            $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
    }
}

// Auth
builder.Services.AddAuth0ApiAuthentication(options =>
{
    options.Domain = builder.Configuration["Auth0:Domain"];
    options.JwtBearerOptions = new JwtBearerOptions
    {
        Audience = builder.Configuration["Auth0:Audience"]
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAuthRequirement.PolicyName,
        policy => policy.Requirements.Add(new AdminAuthRequirement()));
}).AddSingleton<IAuthorizationHandler, AdminAuthHandler>();

builder.Services.AddAuthorization();

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Required connection string 'ConnectionStrings:DefaultConnection' is not configured. " +
        "Set it in configuration or provide it via environment variables before starting the application.");

builder.Services.AddDbContext<TeapotDbContext>(options => options.UseNpgsql(connectionString, o => o
        .MapEnum<EInvitationStatus>("invitation_status")
        .MapEnum<ERole>("role")
        .MapEnum<ETaskPriority>("task_priority")
        .MapEnum<ETaskIntensity>("task_intensity")))
    .AddScoped<IGenericRepository<Invitation>, GenericRepository<Invitation>>()
    .AddScoped<IGenericRepository<Membership>, GenericRepository<Membership>>()
    .AddScoped<IGenericRepository<Organization>, GenericRepository<Organization>>()
    .AddScoped<IGenericRepository<User>, GenericRepository<User>>()
    .AddScoped<IGenericRepository<UserTask>, GenericRepository<UserTask>>()
    .AddScoped<IGenericRepository<WorkProfile>, GenericRepository<WorkProfile>>();

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));

// Services
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();

// User
builder.Services.AddScoped<IUserService, UserService>();

// Tasks
builder.Services.AddScoped<IUserTaskService, UserTaskService>();

// Work Profile
builder.Services.AddScoped<IWorkProfileService, WorkProfileService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(jsonStringEnumConverter);
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

var app = builder.Build();

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