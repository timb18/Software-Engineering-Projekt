using Api.Controller;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        _controller = new AuthController(new GenericRepository<User>(_dbContext));
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
}
