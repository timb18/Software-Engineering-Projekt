using Api.Controller;
using DataAccess;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Api.Tests;

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
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new TeapotDbContext(options);
        var userService = new UserService(
            new UserRepository(_dbContext),
            new OrganizationRepository(_dbContext),
            new MembershipRepository(_dbContext),
            new WorkProfileRepository(_dbContext),
            new UnitOfWork(_dbContext));
        _controller = new AuthController(userService, new UserRepository(_dbContext));
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
        }, CancellationToken.None);

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
        }, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        Assert.That(_dbContext.Users.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task EnsureUser_CreatesMissingUserWithoutPersonalWorkspace()
    {
        var result = await _controller.EnsureUser(
            new EnsureUserRequest("ensure-user@test.com", "auth0|ensure", "Ensure User", "https://example.com/u.png"),
            CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        Assert.That(_dbContext.Users.Count(), Is.EqualTo(1));
        Assert.That(_dbContext.Organizations.Count(), Is.EqualTo(0));
        Assert.That(_dbContext.Memberships.Count(), Is.EqualTo(0));
        Assert.That(_dbContext.WorkProfiles.Count(), Is.EqualTo(0));
        Assert.That(_dbContext.Users.Single().AuthProviderSubject, Is.EqualTo("auth0|ensure"));
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
        Assert.That(_dbContext.Organizations.Count(), Is.EqualTo(0));
        Assert.That(_dbContext.Memberships.Count(), Is.EqualTo(0));
        Assert.That(_dbContext.WorkProfiles.Count(), Is.EqualTo(0));
    }
}
