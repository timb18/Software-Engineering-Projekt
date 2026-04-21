using System.Text.Json;
using System.Text.Json.Serialization;
using DataAccess;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var jsonStringEnumConverter = new JsonStringEnumConverter(
    JsonNamingPolicy.CamelCase,
    false);

// Swagger
builder.Services.AddEndpointsApiExplorer()
    .ConfigureHttpJsonOptions(options => { options.SerializerOptions.Converters.Add(jsonStringEnumConverter); })
    .Configure<JsonOptions>(options => { options.JsonSerializerOptions.Converters.Add(jsonStringEnumConverter); })
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

builder.Services.AddDbContext<TeapotDbContext>(options => options.UseNpgsql(connectionString))
    .AddScoped<IGenericRepository<Invitation>, GenericRepository<Invitation>>()
    .AddScoped<IGenericRepository<Membership>, GenericRepository<Membership>>()
    .AddScoped<IGenericRepository<Organization>, GenericRepository<Organization>>()
    .AddScoped<IGenericRepository<TaskBlock>, GenericRepository<TaskBlock>>()
    .AddScoped<IGenericRepository<TaskDependency>, GenericRepository<TaskDependency>>()
    .AddScoped<IGenericRepository<User>, GenericRepository<User>>()
    .AddScoped<IGenericRepository<UserTask>, GenericRepository<UserTask>>()
    .AddScoped<IGenericRepository<DataAccess.Models.WorkProfile>, GenericRepository<DataAccess.Models.WorkProfile>>()
    .AddScoped<IGenericRepository<WorkProfileTimeInterval>, GenericRepository<WorkProfileTimeInterval>>();

// Work Profile UI feature
builder.Services.AddSingleton<IWorkProfileRepository, InMemoryWorkProfileRepository>();
builder.Services.AddScoped<IWorkProfileService, WorkProfileService>();

builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(jsonStringEnumConverter); });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
    app.UseSwagger()
        .UseSwaggerUI(o =>
        {
            o.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
            o.RoutePrefix = string.Empty;
        });

app.UseCors();
// HTTPS redirect is handled by Render's load balancer; only enable locally.
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.MapControllers();

app.Run();