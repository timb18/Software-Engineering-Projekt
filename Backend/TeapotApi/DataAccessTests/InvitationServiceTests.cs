using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services;

namespace DataAccessTests;

[Category("Integration")]
public class InvitationServiceTests
{
    private TeapotDbContext _dbContext = null!;
    private InvitationService _service = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<TeapotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TeapotDbContext(options);

        _service = new InvitationService(
            new GenericRepository<Invitation>(_dbContext),
            new GenericRepository<Organization>(_dbContext),
            new GenericRepository<User>(_dbContext),
            new GenericRepository<Membership>(_dbContext),
            Options.Create(new EmailOptions
            {
                ApiBaseUrl = "http://localhost:5186",
                FrontendBaseUrl = "http://127.0.0.1:5173/"
            }));
    }

    [Test]
    public async Task SendInvitationAsync_CreatesInvitationAndGeneratesLink()
    {
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            Username = "Organizer",
            CreatedAt = DateTime.UtcNow
        };
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "TeaPot GmbH",
            Description = "Test",
            MaxUsers = 10,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(organizer);
        _dbContext.Organizations.Add(organization);
        _dbContext.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            UserId = organizer.Id,
            OrganizationId = organization.Id,
            Role = ERole.Organizer,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.SendInvitationAsync(
            "member@test.com",
            organization.Id,
            createdByEmail: organizer.Email,
            firstName: "Member",
            lastName: "Test");

        Assert.That(result.Email, Is.EqualTo("member@test.com"));
        Assert.That(result.OrganizationId, Is.EqualTo(organization.Id));
        Assert.That(result.Status, Is.EqualTo("Open"));
        Assert.That(result.InvitationLink, Does.Contain($"/api/Invitation/{result.Id}/accept-link"));
    }

    [Test]
    public async Task AcceptInvitationByEmailAsync_CreatesMembershipAndMarksAccepted()
    {
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            Username = "Organizer",
            CreatedAt = DateTime.UtcNow
        };
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "TeaPot GmbH",
            Description = "Test",
            MaxUsers = 10,
            CreatedAt = DateTime.UtcNow
        };
        var invitedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "member@test.com",
            Username = "Member",
            CreatedAt = DateTime.UtcNow
        };
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CreatedBy = organizer.Id,
            Email = "member@test.com",
            Status = EInvitationStatus.Open,
            CreatedAt = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        };

        _dbContext.Users.Add(organizer);
        _dbContext.Users.Add(invitedUser);
        _dbContext.Organizations.Add(organization);
        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        var accepted = await _service.AcceptInvitationByEmailAsync(invitation.Id, "member@test.com");

        Assert.That(accepted, Is.True);
        Assert.That(_dbContext.Memberships.Count(), Is.EqualTo(1));
        Assert.That(_dbContext.Invitations.Single().Status, Is.EqualTo(EInvitationStatus.Accepted));
    }

    [Test]
    public void AcceptInvitationByEmailAsync_Throws_WhenInvitedUserHasNoAccount()
    {
        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            Username = "Organizer",
            CreatedAt = DateTime.UtcNow
        };
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "TeaPot GmbH",
            Description = "Test",
            MaxUsers = 10,
            CreatedAt = DateTime.UtcNow
        };
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CreatedBy = organizer.Id,
            Email = "member@test.com",
            Status = EInvitationStatus.Open,
            CreatedAt = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        };

        _dbContext.Users.Add(organizer);
        _dbContext.Organizations.Add(organization);
        _dbContext.Invitations.Add(invitation);
        _dbContext.SaveChanges();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.AcceptInvitationByEmailAsync(invitation.Id, "member@test.com"));
    }

    [Test]
    public void SendInvitationAsync_Throws_WhenCreatorIsNotOrganizer()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            Username = "User",
            CreatedAt = DateTime.UtcNow
        };
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "TeaPot GmbH",
            Description = "Test",
            MaxUsers = 10,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        _dbContext.Organizations.Add(organization);
        _dbContext.SaveChanges();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.SendInvitationAsync(
                "member@test.com",
                organization.Id,
                createdByEmail: user.Email));
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }
}
