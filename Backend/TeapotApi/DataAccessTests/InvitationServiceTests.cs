using Api.Dto;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Model;
using NUnit.Framework;
using Services;

namespace DataAccessTests;

[TestFixture]
public class InvitationServiceTests
{
    [Test]
    public async Task CreateInvitationAsync_Creates_Open_Invitation_And_Response_Code()
    {
        await using var context = CreateContext();

        var organizationId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        context.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "TeaPot GmbH",
            Description = "Test org",
            MaxUsers = 10,
            CreatedAt = DateTime.UtcNow
        });

        context.Users.Add(new User
        {
            Id = creatorId,
            Email = "organizer@teapot.de",
            Username = "Organizer",
            CreatedAt = DateTime.UtcNow
        });

        context.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = creatorId,
            Role = ERole.Organizer,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = new InvitationService(context, CreateConfiguration());

        var response = await service.CreateInvitationAsync(new CreateInvitationRequest
        {
            OrganizationId = organizationId,
            CreatedByEmail = "organizer@teapot.de",
            FirstName = "Polina",
            LastName = "Hrybkova",
            Email = "polina@example.com"
        });

        Assert.Multiple(() =>
        {
            Assert.That(response.Email, Is.EqualTo("polina@example.com"));
            Assert.That(response.OrganizationId, Is.EqualTo(organizationId));
            Assert.That(response.Status, Is.EqualTo("open"));
            Assert.That(response.InviteCode, Has.Length.EqualTo(8));
        });

        var storedInvitation = await context.Invitations.SingleAsync();
        Assert.That(storedInvitation.Status, Is.EqualTo(EInvitationStatus.Open));
        Assert.That(storedInvitation.Email, Is.EqualTo("polina@example.com"));
    }

    [Test]
    public async Task AcceptInvitationAsync_Adds_Membership_And_Marks_Invitation_Accepted()
    {
        await using var context = CreateContext();

        var organizationId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();

        context.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "TeaPot GmbH",
            Description = "Test org",
            MaxUsers = 10,
            CreatedAt = DateTime.UtcNow
        });

        context.Users.Add(new User
        {
            Id = creatorId,
            Email = "organizer@teapot.de",
            Username = "Organizer",
            CreatedAt = DateTime.UtcNow
        });

        context.Invitations.Add(new Invitation
        {
            Id = invitationId,
            OrganizationId = organizationId,
            CreatedBy = creatorId,
            Email = "member@teapot.de",
            FirstName = "Member",
            LastName = "Test",
            CreatedAt = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(2),
            Status = EInvitationStatus.Open
        });

        await context.SaveChangesAsync();

        var service = new InvitationService(context, CreateConfiguration());

        await service.AcceptInvitationAsync(invitationId, "member@teapot.de");

        var membership = await context.Memberships.SingleAsync();
        var invitation = await context.Invitations.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(membership.OrganizationId, Is.EqualTo(organizationId));
            Assert.That(membership.Role, Is.EqualTo(ERole.User));
            Assert.That(invitation.Status, Is.EqualTo(EInvitationStatus.Accepted));
        });
    }

    private static TeapotDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TeapotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TeapotDbContext(options);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "http://127.0.0.1:5173/orgs"
            })
            .Build();
    }
}
