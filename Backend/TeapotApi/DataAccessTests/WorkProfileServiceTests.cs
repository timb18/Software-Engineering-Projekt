using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Services;

namespace DataAccessTests;

[TestFixture]
public class WorkProfileServiceTests
{
    private TeapotDbContext _dbContext = null!;
    private WorkProfileService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<TeapotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TeapotDbContext(options);
        _service = new WorkProfileService(
            _dbContext,
            new GenericRepository<WorkProfile>(_dbContext),
            new GenericRepository<Membership>(_dbContext));
    }

    [Test]
    public async Task DeleteAsync_Removes_WorkProfile_And_Dependent_Planning_Data()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "member@example.com",
            Username = "member",
            CreatedAt = DateTime.UtcNow
        };

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Org",
            Description = "Test org",
            MaxUsers = 10,
            CreatedAt = DateTime.UtcNow
        };

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OrganizationId = organization.Id,
            Role = ERole.User,
            CreatedAt = DateTime.UtcNow,
            User = user,
            Organization = organization
        };

        var workProfile = new WorkProfile
        {
            Id = Guid.NewGuid(),
            MembershipId = membership.Id,
            Membership = membership,
            CreatedAt = DateTime.UtcNow,
            MaxDailyLoad = TimeSpan.FromHours(8)
        };

        var workDayProfile = new WorkDayProfile
        {
            Id = Guid.NewGuid(),
            WorkProfileId = workProfile.Id,
            Day = "Mon",
            WorkProfile = workProfile
        };

        var workBlock = new WorkBlock
        {
            Id = Guid.NewGuid(),
            WorkDayProfileId = workDayProfile.Id,
            StartTime = "09:00",
            EndTime = "12:00",
            WorkDayProfile = workDayProfile
        };

        var workBreak = new WorkBreak
        {
            Id = Guid.NewGuid(),
            WorkDayProfileId = workDayProfile.Id,
            StartTime = "10:00",
            EndTime = "10:15",
            WorkDayProfile = workDayProfile
        };

        var userTask = new UserTask
        {
            Id = Guid.NewGuid(),
            WorkProfileId = workProfile.Id,
            Name = "Task",
            CreatedAt = DateTime.UtcNow,
            EarlyStart = DateTime.UtcNow,
            EarlyFinish = DateTime.UtcNow.AddHours(1),
            LateStart = DateTime.UtcNow,
            LateFinish = DateTime.UtcNow.AddHours(1),
            TimeEstimate = TimeSpan.FromHours(1),
            WorkProfile = workProfile
        };

        _dbContext.Users.Add(user);
        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.Add(membership);
        _dbContext.WorkProfiles.Add(workProfile);
        _dbContext.WorkDayProfiles.Add(workDayProfile);
        _dbContext.WorkBlocks.Add(workBlock);
        _dbContext.WorkBreaks.Add(workBreak);
        _dbContext.UserTasks.Add(userTask);
        await _dbContext.SaveChangesAsync();

        await _service.DeleteAsync(user.Id);

        Assert.Multiple(() =>
        {
            Assert.That(_dbContext.WorkProfiles.Any(), Is.False);
            Assert.That(_dbContext.WorkDayProfiles.Any(), Is.False);
            Assert.That(_dbContext.WorkBlocks.Any(), Is.False);
            Assert.That(_dbContext.WorkBreaks.Any(), Is.False);
            Assert.That(_dbContext.UserTasks.Any(), Is.False);
            Assert.That(_dbContext.Memberships.Any(), Is.True);
        });
    }

    [Test]
    public void DeleteAsync_Throws_When_Profile_Does_Not_Exist()
    {
        var act = async () => await _service.DeleteAsync(Guid.NewGuid());

        Assert.ThrowsAsync<KeyNotFoundException>(async () => await act());
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }
}
