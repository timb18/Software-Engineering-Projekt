using Api.Controller;
using Api.Tests.Fakes;
using Auth0.ManagementApi;
using DataAccess;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Api.Tests;

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
            new WorkProfileRepository(_dbContext),
            new UnitOfWork(_dbContext),
            new FakeManagementApiClient([
                new UserResponseSchema { UserId = "a", Email = "new-user@test.com" },
                new UserResponseSchema { UserId = "b", Email = "existing@test.com" },
                new UserResponseSchema { UserId = "c", Email = "ensure-user@test.com" },
                new UserResponseSchema { UserId = "d", Email = "repeat@test.com" }
            ]),
            new Auth0Config("", "", "", "", "", ""));
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
        }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(_dbContext.Users.Count(), Is.EqualTo(1));
            Assert.That(_dbContext.Users.Single().Email, Is.EqualTo("new-user@test.com"));
        });
    }

    [Test]
    public async Task RegisterAsync_ReturnsExistingUser_WhenAlreadyPresent()
    {
        _dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@test.com",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.RegisterAsync(new RegisterRequest
        {
            Email = "existing@test.com"
        }, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        Assert.That(_dbContext.Users.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task EnsureUser_CreatesMissingUserWithoutPersonalWorkspace()
    {
        var result = await _controller.EnsureUser(
            new EnsureUserRequest("ensure-user@test.com", "auth0|ensure"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(_dbContext.Users.Count(), Is.EqualTo(1));
            Assert.That(_dbContext.Organizations.Count(), Is.EqualTo(0));
            Assert.That(_dbContext.Memberships.Count(), Is.EqualTo(0));
            Assert.That(_dbContext.WorkProfiles.Count(), Is.EqualTo(0));
            Assert.That(_dbContext.Users.Single().AuthProviderSubject, Is.EqualTo("auth0|ensure"));
        });
    }

    [Test]
    public async Task EnsureUser_IsIdempotent_ForExistingUser()
    {
        await _controller.EnsureUser(new EnsureUserRequest("repeat@test.com"), CancellationToken.None);

        var secondResult = await _controller.EnsureUser(
            new EnsureUserRequest("repeat@test.com"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(secondResult, Is.InstanceOf<OkObjectResult>());
            Assert.That(_dbContext.Users.Count(), Is.EqualTo(1));
            Assert.That(_dbContext.Organizations.Count(), Is.EqualTo(0));
            Assert.That(_dbContext.Memberships.Count(), Is.EqualTo(0));
            Assert.That(_dbContext.WorkProfiles.Count(), Is.EqualTo(0));
        });
    }
}