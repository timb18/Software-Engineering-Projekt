using DataAccess;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace Services.Tests;

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
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new TeapotDbContext(options);

        _service = new InvitationService(
            new InvitationRepository(_dbContext),
            new OrganizationRepository(_dbContext),
            new UserRepository(_dbContext),
            new MembershipRepository(_dbContext),
            new WorkProfileRepository(_dbContext),
            new UnitOfWork(_dbContext),
            new NullEmailSender(),
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
            7,
            createdByEmail: organizer.Email,
            firstName: "Member",
            lastName: "Test");

        Assert.That(result.Email, Is.EqualTo("member@test.com"));
        Assert.That(result.OrganizationId, Is.EqualTo(organization.Id));
        Assert.That(result.Status, Is.EqualTo("Open"));
        Assert.That(result.InvitationLink, Does.Contain($"/api/Invitation/{result.Id}/accept-link"));
        Assert.That(result.EmailSent, Is.True);
        Assert.That(result.EmailError, Is.Null);
    }

    [Test]
    public async Task SendInvitationAsync_Returns_Link_When_Email_Send_Fails()
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
        var service = new InvitationService(
            new InvitationRepository(_dbContext),
            new OrganizationRepository(_dbContext),
            new UserRepository(_dbContext),
            new MembershipRepository(_dbContext),
            new WorkProfileRepository(_dbContext),
            new UnitOfWork(_dbContext),
            new FailingEmailSender(),
            Options.Create(new EmailOptions { ApiBaseUrl = "http://localhost:5186" }));

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

        var result = await service.SendInvitationAsync(
            "member@test.com",
            organization.Id,
            7,
            createdByEmail: organizer.Email);

        Assert.Multiple(() =>
        {
            Assert.That(result.InvitationLink, Does.Contain($"/api/Invitation/{result.Id}/accept-link"));
            Assert.That(result.EmailSent, Is.False);
            Assert.That(result.EmailError, Does.Contain("SMTP failed"));
            Assert.That(_dbContext.Invitations.Any(i => i.Id == result.Id), Is.True);
        });
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
    public async Task AcceptInvitationByEmailAsync_PreservesPersonalWorkspaceMembership()
    {
        var invitedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "member@test.com",
            Username = "Member",
            CreatedAt = DateTime.UtcNow
        };
        var personalOrganization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Member's Workspace",
            Description = "Personal workspace",
            MaxUsers = 1,
            CreatedAt = DateTime.UtcNow
        };
        var personalMembership = new Membership
        {
            Id = Guid.NewGuid(),
            UserId = invitedUser.Id,
            OrganizationId = personalOrganization.Id,
            Role = ERole.Organizer,
            CreatedAt = DateTime.UtcNow
        };
        var personalWorkProfile = new WorkProfile
        {
            Id = Guid.NewGuid(),
            MembershipId = personalMembership.Id,
            MaxDailyLoad = TimeSpan.FromHours(8),
            PlannerViewStart = "06:00",
            PlannerViewEnd = "22:00",
            CreatedAt = DateTime.UtcNow
        };

        var organizer = new User
        {
            Id = Guid.NewGuid(),
            Email = "organizer@test.com",
            Username = "Organizer",
            CreatedAt = DateTime.UtcNow
        };
        var invitedOrganization = new Organization
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
            OrganizationId = invitedOrganization.Id,
            CreatedBy = organizer.Id,
            Email = invitedUser.Email,
            Status = EInvitationStatus.Open,
            CreatedAt = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        };

        _dbContext.Users.AddRange(organizer, invitedUser);
        _dbContext.Organizations.AddRange(personalOrganization, invitedOrganization);
        _dbContext.Memberships.Add(personalMembership);
        _dbContext.WorkProfiles.Add(personalWorkProfile);
        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        var accepted = await _service.AcceptInvitationByEmailAsync(invitation.Id, invitedUser.Email);

        Assert.That(accepted, Is.True);
        Assert.That(_dbContext.Memberships.Count(), Is.EqualTo(2));
        Assert.That(_dbContext.WorkProfiles.Count(), Is.EqualTo(2), "A new WorkProfile must be created for the org membership");
        Assert.That(_dbContext.Memberships.Count(m => m.UserId == invitedUser.Id && m.OrganizationId == invitedOrganization.Id), Is.EqualTo(1));
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
                7,
                createdByEmail: user.Email));
    }

    [Test]
    public async Task RejectInvitationAsync_DeletesInvitation()
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
        await _dbContext.SaveChangesAsync();

        await _service.RejectInvitationAsync(invitation.Id);

        Assert.That(await _dbContext.Invitations.AnyAsync(i => i.Id == invitation.Id), Is.False);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }
}

public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class FailingEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("SMTP failed");
}
