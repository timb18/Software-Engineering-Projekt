using Cli;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services;

namespace CliTests;
[Category("Integration")]
public class CliTests
{
    private CommandParser _parser;
    [SetUp]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.Development.json", true).AddEnvironmentVariables().Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        var services = new ServiceCollection();
        services.AddTeapotServices();
        services.AddDbContext<TeapotDbContext>(options => options.UseNpgsql(connectionString, o => o
                .MapEnum<EInvitationStatus>("invitation_status")
                .MapEnum<ERole>("role")
                .MapEnum<ETaskPriority>("task_priority")
                .MapEnum<ETaskIntensity>("task_intensity")))
            .AddScoped<IGenericRepository<Membership>, GenericRepository<Membership>>()
            .AddScoped<IGenericRepository<Organization>, GenericRepository<Organization>>()
            .AddScoped<IGenericRepository<User>, GenericRepository<User>>();
        using ServiceProvider provider = services.BuildServiceProvider();
        {
            
        }
        _parser = new CommandParser(provider);
    }

    [Test]
    public async Task CreateOrganization_Success()
    {
        string[] args = { "create-org", "--name", "organization", "--description", "An Organization", "--max-members", "20", "--organizer-username", "John Pork", "--organizer-email", "JohnPork@email.com"};
        
        var result = await _parser.ParseCommand(args);

        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public async Task CreateOrganization_Failure_ZeroArgs()
    {
        string[] args = { };
        Assert.That(async () => await _parser.ParseCommand(args),
            Throws.TypeOf<Exception>()
                .With.Message.EqualTo($"No Arguments provided.")
        );
    }

    [Test]
    public async Task CreateOrganization_Failure_UnknownCommand()
    {
        string[] args = { "create-team" };
        Assert.That(async () => await _parser.ParseCommand(args),
            Throws.TypeOf<Exception>()
                .With.Message.EqualTo($"Unknown command: {args[0]}"));
    }

    [Test]
    public async Task CreateOrganization_Failure_InvalidArguments()
    {
        string[] args = { "create-org", "meow" };
        Assert.That(async () => await _parser.ParseCommand(args),
            Throws.TypeOf<Exception>()
                .With.Message.EqualTo("Invalid command line arguments. Check Console for more information.")
        );
    }

    [Test]
    public async Task CreateOrganization_Failure_EmptyArguments()
    {
        string[] args = { "create-org", "--name", " ", "--description", "An Organization", "--max-members", "20", "--organizer-username", "John Pork", "--organizer-email", "JohnPork@email.com" };
        Assert.That(async () => await _parser.ParseCommand(args),
            Throws.TypeOf<Exception>()
                .With.Message
                .EqualTo($"Invalid command line arguments. Make sure you filled out all required parameters.")
        );
    }

    [Test]
    public async Task CreateOrganization_Failure_InvalidMaxMembers()
    {
        string[] args =
        {
            "create-org", "--name", "organization", "--description", "An Organization", "--max-members", "cat",
            "--organizer-username", "John Pork", "--organizer-email", "JohnPork@email.com"
        };
        Assert.That(async () => await _parser.ParseCommand(args),
            Throws.TypeOf<Exception>()
                .With.Message.EqualTo($"Argument for max-members must be a whole positive number.")
        );
    }

    [Test]
    public async Task CreateOrganization_Failure_NegativeMaxMembers()
    {
        string[] args = {
            "create-org", "--name", "organization", "--description", "An Organization", "--max-members", "-20",
            "--organizer-username", "John Pork", "--organizer-email", "JohnPork@email.com"
        };
        Assert.That(async () => await _parser.ParseCommand(args),
            Throws.TypeOf<Exception>()
                .With.Message.EqualTo($"Failed to create organization. Check the console for more information.")
        );
    }
}