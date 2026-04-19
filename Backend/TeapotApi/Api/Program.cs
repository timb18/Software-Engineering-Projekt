using System.Text.Json;
using System.Text.Json.Serialization;
using DataAccess.Models;
using DataAccess.Repositories;
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
        o.NonNullableReferenceTypesAsRequired();
        o.SupportNonNullableReferenceTypes();
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
        connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
    }
}

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
app.MapControllers();

app.Run();