using Api.Controller;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services;

namespace DataAccessTests;

[TestFixture]
public class UserControllerTests
{
    private TeapotDbContext _dbContext = null!;
    private UserController _controller = null!;

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

        _controller = new UserController(userService);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    [Test]
    public async Task GetProfile_Returns_Ok_For_Existing_User()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "profile@test.com",
            Username = "profile",
            DisplayName = "Profile User",
            Timezone = "Europe/Berlin",
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.GetProfile(user.Id, CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkObjectResult>());
    }

    [Test]
    public async Task UpdateProfile_Returns_BadRequest_For_Invalid_Email()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "profile@test.com",
            Username = "profile",
            DisplayName = "Profile User",
            Timezone = "Europe/Berlin",
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.UpdateProfile(
            user.Id,
            new UpdateUserProfileRequest("Profile User", "not-an-email", null, "Europe/Berlin"),
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task UpdateProfile_Returns_Ok_And_Updates_User()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "profile@test.com",
            Username = "profile",
            DisplayName = "Profile User",
            Timezone = "Europe/Berlin",
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await _controller.UpdateProfile(
            user.Id,
            new UpdateUserProfileRequest(
                "Updated Profile",
                "updated@test.com",
                "https://example.com/avatar.png",
                "Europe/Paris"),
            CancellationToken.None);

        var saved = await _dbContext.Users.FindAsync(user.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            Assert.That(saved!.DisplayName, Is.EqualTo("Updated Profile"));
            Assert.That(saved.Email, Is.EqualTo("updated@test.com"));
            Assert.That(saved.Timezone, Is.EqualTo("Europe/Paris"));
        });
    }
}
