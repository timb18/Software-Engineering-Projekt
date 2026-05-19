using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Services.Tests;

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
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new TeapotDbContext(options);
        _service = new WorkProfileService(
            new WorkProfileRepository(_dbContext),
            new MembershipRepository(_dbContext));
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
    public async Task GetAsync_Returns_Null_After_Profile_Delete_So_Client_Can_Reset_To_Defaults()
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

        _dbContext.Users.Add(user);
        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.Add(membership);
        _dbContext.WorkProfiles.Add(workProfile);
        await _dbContext.SaveChangesAsync();

        await _service.DeleteAsync(user.Id);

        var deletedProfile = await _service.GetAsync(user.Id);

        Assert.That(deletedProfile, Is.Null);
    }

    [Test]
    public void DeleteAsync_Throws_When_Profile_Does_Not_Exist()
    {
        var act = async () => await _service.DeleteAsync(Guid.NewGuid());

        Assert.ThrowsAsync<KeyNotFoundException>(async () => await act());
    }

    [Test]
    public async Task DeleteByEmailAsync_Removes_WorkProfile_For_Email()
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

        _dbContext.Users.Add(user);
        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.Add(membership);
        _dbContext.WorkProfiles.Add(workProfile);
        await _dbContext.SaveChangesAsync();

        await _service.DeleteByEmailAsync(user.Email);

        Assert.That(_dbContext.WorkProfiles.Any(), Is.False);
    }

    [Test]
    public async Task SaveAsync_Preserves_Block_Organization_Assignment_For_Multiple_Organizations()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "member@example.com",
            Username = "member",
            CreatedAt = DateTime.UtcNow
        };

        var personalOrganization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Personal workspace",
            Description = "Personal workspace",
            MaxUsers = 1,
            CreatedAt = DateTime.UtcNow
        };

        var teamOrganization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Team Org",
            Description = "Shared team",
            MaxUsers = 10,
            CreatedAt = DateTime.UtcNow
        };

        var personalMembership = new Membership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OrganizationId = personalOrganization.Id,
            Role = ERole.Organizer,
            CreatedAt = DateTime.UtcNow,
            User = user,
            Organization = personalOrganization
        };

        var teamMembership = new Membership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OrganizationId = teamOrganization.Id,
            Role = ERole.User,
            CreatedAt = DateTime.UtcNow,
            User = user,
            Organization = teamOrganization
        };

        _dbContext.Users.Add(user);
        _dbContext.Organizations.AddRange(personalOrganization, teamOrganization);
        _dbContext.Memberships.AddRange(personalMembership, teamMembership);
        await _dbContext.SaveChangesAsync();

        var profile = new WorkProfile
        {
            Days =
            [
                new WorkDayProfile
                {
                    Day = "Mon",
                    Blocks =
                    [
                        new WorkBlock
                        {
                            CompanyId = teamOrganization.Id.ToString(),
                            CompanyName = teamOrganization.Name,
                            StartTime = "09:00",
                            EndTime = "17:00"
                        }
                    ],
                    Breaks = []
                }
            ]
        };

        await _service.SaveAsync(user.Id, profile);
        var saved = await _service.GetAsync(user.Id);

        var mondayBlock = saved!.Days.Single(day => day.Day == "Mon").Blocks.Single();
        Assert.Multiple(() =>
        {
            Assert.That(mondayBlock.CompanyId, Is.EqualTo(teamOrganization.Id.ToString()));
            Assert.That(mondayBlock.CompanyName, Is.EqualTo(teamOrganization.Name));
        });
    }

    [Test]
    public async Task SaveAsync_Updates_Persisted_Profile_And_Replaces_Removed_Blocks()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "member@example.com",
            Username = "member",
            CreatedAt = DateTime.UtcNow
        };

        var personalOrganization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Personal workspace",
            Description = "Personal workspace",
            MaxUsers = 1,
            CreatedAt = DateTime.UtcNow
        };

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OrganizationId = personalOrganization.Id,
            Role = ERole.Organizer,
            CreatedAt = DateTime.UtcNow,
            User = user,
            Organization = personalOrganization
        };

        _dbContext.Users.Add(user);
        _dbContext.Organizations.Add(personalOrganization);
        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        await _service.SaveAsync(user.Id, new WorkProfile
        {
            PlannerViewStart = "06:00",
            PlannerViewEnd = "22:00",
            MaxDailyLoad = TimeSpan.FromHours(8),
            Days =
            [
                new WorkDayProfile
                {
                    Day = "Mon",
                    Blocks =
                    [
                        new WorkBlock
                        {
                            CompanyId = personalOrganization.Id.ToString(),
                            CompanyName = personalOrganization.Name,
                            StartTime = "09:00",
                            EndTime = "12:00"
                        }
                    ],
                    Breaks = []
                }
            ]
        });

        var savedProfile = await _dbContext.WorkProfiles.SingleAsync();
        var task = new UserTask
        {
            Id = Guid.NewGuid(),
            WorkProfileId = savedProfile.Id,
            Name = "Scheduled task",
            CreatedAt = DateTime.UtcNow,
            TimeEstimate = TimeSpan.FromHours(1),
            EarlyStart = DateTime.UtcNow,
            EarlyFinish = DateTime.UtcNow.AddHours(1),
            LateStart = DateTime.UtcNow,
            LateFinish = DateTime.UtcNow.AddHours(1)
        };
        _dbContext.UserTasks.Add(task);
        _dbContext.TaskBlocks.Add(new TaskBlock
        {
            TaskId = task.Id,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(1),
            IsFixed = false
        });
        await _dbContext.SaveChangesAsync();

        var updated = await _service.SaveAsync(user.Id, new WorkProfile
        {
            PlannerViewStart = "07:00",
            PlannerViewEnd = "21:00",
            MaxDailyLoad = TimeSpan.FromHours(6),
            Days =
            [
                new WorkDayProfile
                {
                    Day = "Tue",
                    Blocks =
                    [
                        new WorkBlock
                        {
                            CompanyId = personalOrganization.Id.ToString(),
                            CompanyName = personalOrganization.Name,
                            StartTime = "13:00",
                            EndTime = "17:00"
                        }
                    ],
                    Breaks =
                    [
                        new WorkBreak
                        {
                            StartTime = "15:00",
                            EndTime = "15:30"
                        }
                    ]
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(updated.PlannerViewStart, Is.EqualTo("07:00"));
            Assert.That(updated.PlannerViewEnd, Is.EqualTo("21:00"));
            Assert.That(updated.MaxDailyLoad, Is.EqualTo(TimeSpan.FromHours(6)));
            Assert.That(updated.Days.Single(day => day.Day == "Mon").Blocks, Is.Empty);
            Assert.That(updated.Days.Single(day => day.Day == "Tue").Blocks.Select(block => block.StartTime), Is.EqualTo(["13:00"]));
            Assert.That(updated.Days.Single(day => day.Day == "Tue").Breaks.Select(workBreak => workBreak.StartTime), Is.EqualTo(["15:00"]));
            Assert.That(_dbContext.TaskBlocks.Where(block => !block.IsFixed), Is.Empty);
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }
}
