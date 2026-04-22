using Cli;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services;


var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.Development.json", true).AddEnvironmentVariables().Build();
var connectionString = configuration.GetConnectionString("DefaultConnection");
        
var services = new ServiceCollection();
services.AddTeapotServices();
services.AddDbContext<TeapotDbContext>(options => options.UseNpgsql(connectionString, o => o
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
var provider = services.BuildServiceProvider();
var context = provider.GetRequiredService<TeapotDbContext>();
context.Database.EnsureCreated();
var parser = new CommandParser(provider);

while (true)
{
    try
    {
        var input = Console.ReadLine()?.Split(' ') ?? Array.Empty<string>();
        await parser.ParseCommand(input);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
    }
}
