using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Services.Tests;

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
        _service = new OrganizationService(
            new OrganizationRepository(_dbContext),
            Options.Create(new EmailOptions { ApiBaseUrl = "https://api.example.test" }));
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
    public async Task GetOrganizationsForUserAsync_Includes_CurrentUsers_WorkProfileId()
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
            Name = "Team Org",
            Description = "Test",
            MaxUsers = 5,
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

        _dbContext.Users.Add(user);
        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.Add(membership);
        _dbContext.WorkProfiles.Add(workProfile);
        await _dbContext.SaveChangesAsync();

        var result = (await _service.GetOrganizationsForUserAsync(user.Email)).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].WorkProfileId, Is.EqualTo(workProfile.Id));
    }

    [Test]
    public async Task GetOrganizationsForUserAsync_Normalizes_Email_For_Lookup_And_WorkProfile_Mapping()
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
            Name = "Team Org",
            Description = "Test",
            MaxUsers = 5,
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

        _dbContext.Users.Add(user);
        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.Add(membership);
        _dbContext.WorkProfiles.Add(workProfile);
        await _dbContext.SaveChangesAsync();

        var result = (await _service.GetOrganizationsForUserAsync("  MEMBER@example.COM  ")).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].WorkProfileId, Is.EqualTo(workProfile.Id));
    }

    [Test]
    public async Task GetOrganizationsForUserAsync_Returns_All_Organizations_For_Membership_User()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "member@example.com",
            Username = "member",
            CreatedAt = DateTime.UtcNow
        };

        var orgA = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Org A",
            Description = "A",
            MaxUsers = 5,
            CreatedAt = DateTime.UtcNow
        };
        var orgB = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Org B",
            Description = "B",
            MaxUsers = 5,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        _dbContext.Organizations.AddRange(orgA, orgB);
        _dbContext.Memberships.AddRange(
            new Membership
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OrganizationId = orgA.Id,
                Role = ERole.User,
                CreatedAt = DateTime.UtcNow,
                User = user,
                Organization = orgA
            },
            new Membership
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OrganizationId = orgB.Id,
                Role = ERole.Organizer,
                CreatedAt = DateTime.UtcNow,
                User = user,
                Organization = orgB
            });
        await _dbContext.SaveChangesAsync();

        var result = (await _service.GetOrganizationsForUserAsync(user.Email)).ToList();

        Assert.That(result.Select(x => x.Name), Is.EquivalentTo(new[] { "Org A", "Org B" }));
    }

    [Test]
    public async Task GetOrganizationsForUserAsync_Includes_Invitation_Link_For_Open_Invites()
    {
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@example.com",
            Username = "organizer",
            CreatedAt = DateTime.UtcNow
        };
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Team Org",
            Description = "Test",
            MaxUsers = 5,
            CreatedAt = DateTime.UtcNow
        };
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CreatedBy = organizer.Id,
            Email = "invitee@example.com",
            Status = EInvitationStatus.Open,
            CreatedAt = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        };

        _dbContext.Users.Add(organizer);
        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            UserId = organizer.Id,
            OrganizationId = organization.Id,
            Role = ERole.Organizer,
            CreatedAt = DateTime.UtcNow,
            User = organizer,
            Organization = organization
        });
        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        var result = (await _service.GetOrganizationsForUserAsync(organizer.Email)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.Invites, Has.Count.EqualTo(1));
            Assert.That(result.Invites[0].InvitationLink, Does.StartWith("https://api.example.test/api/Invitation/"));
            Assert.That(result.Invites[0].InvitationLink, Does.Contain(invitation.Id.ToString()));
            Assert.That(result.Invites[0].InvitationLink, Does.Contain("email=invitee%40example.com"));
        });
    }

    [Test]
    public void DeleteOrganizationAsync_Throws_When_Organization_Has_Additional_Organizers()
    {
        var organizer = new User { Id = Guid.NewGuid(), Email = "organizer@example.com", Username = "org", CreatedAt = DateTime.UtcNow };
        var coOrganizer = new User { Id = Guid.NewGuid(), Email = "co-organizer@example.com", Username = "co", CreatedAt = DateTime.UtcNow };
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Team Org", Description = "Test", MaxUsers = 5, CreatedAt = DateTime.UtcNow };

        _dbContext.Users.AddRange(organizer, coOrganizer);
        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.AddRange(
            new Membership { Id = Guid.NewGuid(), UserId = organizer.Id, OrganizationId = organization.Id, Role = ERole.Organizer, CreatedAt = DateTime.UtcNow },
            new Membership { Id = Guid.NewGuid(), UserId = coOrganizer.Id, OrganizationId = organization.Id, Role = ERole.Organizer, CreatedAt = DateTime.UtcNow });
        _dbContext.SaveChanges();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.DeleteOrganizationAsync(new DeleteOrganizationCommand(organization.Id, organizer.Id, organization.Name)));
    }

    [Test]
    public async Task DeleteOrganizationAsync_Removes_Organization_And_All_Members_When_Only_Organizer()
    {
        var organizer = new User { Id = Guid.NewGuid(), Email = "organizer@example.com", Username = "org", CreatedAt = DateTime.UtcNow };
        var member = new User { Id = Guid.NewGuid(), Email = "member@example.com", Username = "member", CreatedAt = DateTime.UtcNow };
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Team Org", Description = "Test", MaxUsers = 5, CreatedAt = DateTime.UtcNow };

        _dbContext.Users.AddRange(organizer, member);
        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.AddRange(
            new Membership { Id = Guid.NewGuid(), UserId = organizer.Id, OrganizationId = organization.Id, Role = ERole.Organizer, CreatedAt = DateTime.UtcNow },
            new Membership { Id = Guid.NewGuid(), UserId = member.Id, OrganizationId = organization.Id, Role = ERole.User, CreatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        await _service.DeleteOrganizationAsync(new DeleteOrganizationCommand(organization.Id, organizer.Id, organization.Name));

        Assert.Multiple(() =>
        {
            Assert.That(_dbContext.Organizations.Any(), Is.False);
            Assert.That(_dbContext.Memberships.Any(), Is.False);
        });
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
