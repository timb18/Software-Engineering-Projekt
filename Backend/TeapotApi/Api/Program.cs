using System.Text.Json;
using System.Text.Json.Serialization;
using DataAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var jsonStringEnumConverter = new JsonStringEnumConverter(
    JsonNamingPolicy.CamelCase,
    false);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var databaseUrl = builder.Configuration["DATABASE_URL"] ?? Environment.GetEnvironmentVariable("DATABASE_URL");

if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<TeapotDbContext>(options => options.UseNpgsql(connectionString));
}
else if (!string.IsNullOrWhiteSpace(databaseUrl))
{
    builder.Services.AddDbContext<TeapotDbContext>(options => options.UseNpgsql(ParseDatabaseUrl(databaseUrl)));
}
else
{
    builder.Services.AddDbContext<TeapotDbContext>(options => options.UseInMemoryDatabase("teapot-local"));
}

builder.Services.AddEndpointsApiExplorer()
    .ConfigureHttpJsonOptions(options => { options.SerializerOptions.Converters.Add(jsonStringEnumConverter); })
    .Configure<JsonOptions>(options => { options.JsonSerializerOptions.Converters.Add(jsonStringEnumConverter); })
    .AddSwaggerGen(o =>
    {
        o.SwaggerDoc("v1",
            new OpenApiInfo
            { Title = "OfficeDashboardApi", Version = "v1", Description = "Backend API for the Office Dashboard" });
    })
    .AddCors(options => options.AddDefaultPolicy(c => { c.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader(); }));

builder.Services.AddControllers();
builder.Services.AddScoped<InvitationService>();
builder.Services.AddScoped<OrganizationQueryService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger()
        .UseSwaggerUI(o =>
        {
            o.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
            o.RoutePrefix = string.Empty;
        });
}

app.UseCors();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

static string ParseDatabaseUrl(string databaseUrl)
{
    if (databaseUrl.Contains("Host=", StringComparison.OrdinalIgnoreCase))
    {
        return databaseUrl;
    }

    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
    var database = uri.AbsolutePath.TrimStart('/');

    return
        $"Host={uri.Host};Port={uri.Port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
}
