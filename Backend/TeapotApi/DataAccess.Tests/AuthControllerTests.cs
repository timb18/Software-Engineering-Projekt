using Api.Controller;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services;

namespace DataAccessTests;

[Category("Integration")]
public class AuthControllerTests
{
    private TeapotDbContext _dbContext = null!;
    private AuthController _controller = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<TeapotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TeapotDbContext(options);
        var userService = new UserService(
            new GenericRepository<User>(_dbContext),
            new GenericRepository<Organization>(_dbContext),
            new GenericRepository<Membership>(_dbContext),
            new GenericRepository<WorkProfile>(_dbContext),
            _dbContext);
        _controller = new AuthController(userService, new GenericRepository<User>(_dbContext));
    }

    [TearDown]
    public void Dispose()
    {
        _dbContext?.Dispose();
    }

    [Test]
    public async Task RegisterAsync_CreatesMissingUser()
    {
        var result = await _controller.RegisterAsync(new RegisterRequest
        {
            Email = "new-user@test.com",
            Username = "new-user"
        });

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        Assert.That(_dbContext.Users.Count(), Is.EqualTo(1));
        Assert.That(_dbContext.Users.Single().Email, Is.EqualTo("new-user@test.com"));
    }

    [Test]
    public async Task RegisterAsync_ReturnsExistingUser_WhenAlreadyPresent()
    {
        _dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@test.com",
            Username = "existing",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.RegisterAsync(new RegisterRequest
        {
            Email = "existing@test.com",
            Username = "ignored"
        });

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        Assert.That(_dbContext.Users.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task EnsureUser_CreatesMissingUserAndWorkProfile()
    {
        var result = await _controller.EnsureUser(
            new EnsureUserRequest("ensure-user@test.com"),
            CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        Assert.That(_dbContext.Users.Count(), Is.EqualTo(1));
        Assert.That(_dbContext.Organizations.Count(), Is.EqualTo(1));
        Assert.That(_dbContext.Memberships.Count(), Is.EqualTo(1));
        Assert.That(_dbContext.WorkProfiles.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task EnsureUser_IsIdempotent_ForExistingUser()
    {
        await _controller.EnsureUser(new EnsureUserRequest("repeat@test.com"), CancellationToken.None);

        var secondResult = await _controller.EnsureUser(
            new EnsureUserRequest("repeat@test.com"),
            CancellationToken.None);

        Assert.That(secondResult, Is.InstanceOf<OkObjectResult>());
        Assert.That(_dbContext.Users.Count(), Is.EqualTo(1));
        Assert.That(_dbContext.Organizations.Count(), Is.EqualTo(1));
        Assert.That(_dbContext.Memberships.Count(), Is.EqualTo(1));
        Assert.That(_dbContext.WorkProfiles.Count(), Is.EqualTo(1));
    }
}
