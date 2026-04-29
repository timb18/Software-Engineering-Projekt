using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Services;

namespace DataAccessTests;

[TestFixture]
public class OrganizationServiceTests
{
    private TeapotDbContext _dbContext = null!;
    private OrganizationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<TeapotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TeapotDbContext(options);
        _service = new OrganizationService(new GenericRepository<Organization>(_dbContext), _dbContext);
    }

    [TearDown]
    public async Task TearDown() => await _dbContext.DisposeAsync();

    [Test]
    public async Task DeleteOrganizationAsync_Removes_Organization_And_Dependent_Data()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@example.com",
            Username = "organizer",
            CreatedAt = DateTime.UtcNow
        };
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Personal",
            Description = "Test org",
            MaxUsers = 1,
            CreatedAt = DateTime.UtcNow
        };
        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OrganizationId = organization.Id,
            Role = ERole.Organizer,
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
        var workDay = new WorkDayProfile
        {
            Id = Guid.NewGuid(),
            WorkProfileId = workProfile.Id,
            Day = "Mon",
            WorkProfile = workProfile
        };
        var workBlock = new WorkBlock
        {
            Id = Guid.NewGuid(),
            WorkDayProfileId = workDay.Id,
            StartTime = "09:00",
            EndTime = "17:00",
            WorkDayProfile = workDay
        };
        var workBreak = new WorkBreak
        {
            Id = Guid.NewGuid(),
            WorkDayProfileId = workDay.Id,
            StartTime = "12:00",
            EndTime = "12:30",
            WorkDayProfile = workDay
        };
        var task = new UserTask
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
            Priority = ETaskPriority.Medium,
            Intensity = ETaskIntensity.Normal,
            IsFixed = false
        };
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CreatedBy = user.Id,
            Email = "invitee@example.com",
            Status = EInvitationStatus.Open,
            CreatedAt = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        };

        _dbContext.Users.Add(user);
        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.Add(membership);
        _dbContext.WorkProfiles.Add(workProfile);
        _dbContext.WorkDayProfiles.Add(workDay);
        _dbContext.WorkBlocks.Add(workBlock);
        _dbContext.WorkBreaks.Add(workBreak);
        _dbContext.UserTasks.Add(task);
        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        await _service.DeleteOrganizationAsync(new DeleteOrganizationCommand(organization.Id, user.Id, organization.Name));

        Assert.Multiple(() =>
        {
            Assert.That(_dbContext.Organizations.Any(), Is.False);
            Assert.That(_dbContext.Memberships.Any(), Is.False);
            Assert.That(_dbContext.WorkProfiles.Any(), Is.False);
            Assert.That(_dbContext.WorkDayProfiles.Any(), Is.False);
            Assert.That(_dbContext.WorkBlocks.Any(), Is.False);
            Assert.That(_dbContext.WorkBreaks.Any(), Is.False);
            Assert.That(_dbContext.UserTasks.Any(), Is.False);
            Assert.That(_dbContext.Invitations.Any(), Is.False);
        });
    }

    [Test]
    public void DeleteOrganizationAsync_Throws_When_Organization_Has_Additional_Members()
    {
        var organizer = new User { Id = Guid.NewGuid(), Email = "organizer@example.com", Username = "org", CreatedAt = DateTime.UtcNow };
        var member = new User { Id = Guid.NewGuid(), Email = "member@example.com", Username = "member", CreatedAt = DateTime.UtcNow };
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Team Org", Description = "Test", MaxUsers = 5, CreatedAt = DateTime.UtcNow };

        _dbContext.Users.AddRange(organizer, member);
        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.AddRange(
            new Membership { Id = Guid.NewGuid(), UserId = organizer.Id, OrganizationId = organization.Id, Role = ERole.Organizer, CreatedAt = DateTime.UtcNow },
            new Membership { Id = Guid.NewGuid(), UserId = member.Id, OrganizationId = organization.Id, Role = ERole.User, CreatedAt = DateTime.UtcNow });
        _dbContext.SaveChanges();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.DeleteOrganizationAsync(new DeleteOrganizationCommand(organization.Id, organizer.Id, organization.Name)));
    }

    [Test]
    public void DeleteOrganizationAsync_Throws_When_Confirmation_Does_Not_Match()
    {
        var organizer = new User { Id = Guid.NewGuid(), Email = "organizer@example.com", Username = "org", CreatedAt = DateTime.UtcNow };
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Team Org", Description = "Test", MaxUsers = 5, CreatedAt = DateTime.UtcNow };

        _dbContext.Users.Add(organizer);
        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.Add(new Membership { Id = Guid.NewGuid(), UserId = organizer.Id, OrganizationId = organization.Id, Role = ERole.Organizer, CreatedAt = DateTime.UtcNow });
        _dbContext.SaveChanges();

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.DeleteOrganizationAsync(new DeleteOrganizationCommand(organization.Id, organizer.Id, "wrong name")));
    }
}
