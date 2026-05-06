using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Services.Tests;

[TestFixture]
public class MembershipServiceTests
{
    private TeapotDbContext _dbContext = null!;
    private MembershipService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<TeapotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TeapotDbContext(options);
        _service = new MembershipService(new MembershipRepository(_dbContext));
    }

    [Test]
    public async Task LeaveOrganizationAsync_Removes_Membership_And_Dependent_WorkProfile_Data()
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
            CreatedAt = DateTime.UtcNow
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

        await _service.LeaveOrganizationAsync(user.Id, organization.Id);

        var hasMemberships = await _dbContext.Memberships.AnyAsync();
        var hasWorkProfiles = await _dbContext.WorkProfiles.AnyAsync();
        var hasWorkDayProfiles = await _dbContext.WorkDayProfiles.AnyAsync();
        var hasWorkBlocks = await _dbContext.WorkBlocks.AnyAsync();
        var hasWorkBreaks = await _dbContext.WorkBreaks.AnyAsync();
        var hasUserTasks = await _dbContext.UserTasks.AnyAsync();
        Assert.Multiple(() =>
        {
            Assert.That(hasMemberships, Is.False);
            Assert.That(hasWorkProfiles, Is.False);
            Assert.That(hasWorkDayProfiles, Is.False);
            Assert.That(hasWorkBlocks, Is.False);
            Assert.That(hasWorkBreaks, Is.False);
            Assert.That(hasUserTasks, Is.False);
        });
    }

    [Test]
    public void LeaveOrganizationAsync_Throws_When_Membership_Does_Not_Exist()
    {
        var act = async () => await _service.LeaveOrganizationAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.ThrowsAsync<KeyNotFoundException>(async () => await act());
    }

    [Test]
    public async Task UpdateRoleAsync_Persists_New_Role_When_Initiator_Is_Organizer()
    {
        var initiator = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@example.com",
            Username = "organizer",
            CreatedAt = DateTime.UtcNow
        };
        var member = new User
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

        _dbContext.Users.AddRange(initiator, member);
        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.AddRange(
            new Membership
            {
                Id = Guid.NewGuid(),
                UserId = initiator.Id,
                OrganizationId = organization.Id,
                Role = ERole.Organizer,
                CreatedAt = DateTime.UtcNow
            },
            new Membership
            {
                Id = Guid.NewGuid(),
                UserId = member.Id,
                OrganizationId = organization.Id,
                Role = ERole.User,
                CreatedAt = DateTime.UtcNow
            });
        await _dbContext.SaveChangesAsync();

        await _service.UpdateRoleAsync(initiator.Id, member.Id, organization.Id, "admin");

        var updatedMembership = await _dbContext.Memberships
            .SingleAsync(m => m.UserId == member.Id && m.OrganizationId == organization.Id);
        Assert.That(updatedMembership.Role, Is.EqualTo(ERole.Organizer));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }
}
