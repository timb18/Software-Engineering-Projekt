using Api.Controller;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests;

[TestFixture]
public class MembershipControllerTests
{
    [Test]
    public async Task LeaveOrganizationAsync_Returns_NotFound_When_Membership_Is_Missing()
    {
        var controller = new MembershipController(new StubMembershipService
        {
            ExceptionToThrow = new KeyNotFoundException()
        });

        var result = await controller.LeaveOrganizationAsync(
            new RemoveMembershipRequest
            {
                UserId = Guid.NewGuid().ToString(),
                OrganizationId = Guid.NewGuid().ToString()
            },
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task RemoveUserFromOrganizationAsync_Returns_BadRequest_When_InitiatorUserId_Is_Invalid()
    {
        var controller = new MembershipController(new StubMembershipService());

        var result = await controller.RemoveUserFromOrganizationAsync(
            new RemoveUserRequest
            {
                InitiatorUserId = "not-a-guid",
                UserId = Guid.NewGuid().ToString(),
                OrganizationId = Guid.NewGuid().ToString()
            },
            CancellationToken.None);

        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest, Is.Not.Null);
        Assert.That(badRequest!.Value, Is.EqualTo("InitiatorUserId must be a valid GUID."));
    }

    [Test]
    public async Task RemoveUserFromOrganizationAsync_Returns_BadRequest_When_UserId_Is_Invalid()
    {
        var controller = new MembershipController(new StubMembershipService());

        var result = await controller.RemoveUserFromOrganizationAsync(
            new RemoveUserRequest
            {
                InitiatorUserId = Guid.NewGuid().ToString(),
                UserId = "not-a-guid",
                OrganizationId = Guid.NewGuid().ToString()
            },
            CancellationToken.None);

        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest, Is.Not.Null);
        Assert.That(badRequest!.Value, Is.EqualTo("UserId must be a valid GUID."));
    }

    [Test]
    public async Task RemoveUserFromOrganizationAsync_Returns_BadRequest_When_OrganizationId_Is_Invalid()
    {
        var controller = new MembershipController(new StubMembershipService());

        var result = await controller.RemoveUserFromOrganizationAsync(
            new RemoveUserRequest
            {
                InitiatorUserId = Guid.NewGuid().ToString(),
                UserId = Guid.NewGuid().ToString(),
                OrganizationId = "org-a"
            },
            CancellationToken.None);

        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest, Is.Not.Null);
        Assert.That(badRequest!.Value, Is.EqualTo("OrganizationId must be a valid GUID."));
    }

    [Test]
    public async Task RemoveUserFromOrganizationAsync_Returns_Forbidden_When_Initiator_Is_Not_Organizer()
    {
        var controller = new MembershipController(new StubMembershipService
        {
            ExceptionToThrow = new UnauthorizedAccessException()
        });

        var result = await controller.RemoveUserFromOrganizationAsync(
            new RemoveUserRequest
            {
                InitiatorUserId = Guid.NewGuid().ToString(),
                UserId = Guid.NewGuid().ToString(),
                OrganizationId = Guid.NewGuid().ToString()
            },
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task RemoveUserFromOrganizationAsync_Returns_NotFound_When_Membership_Is_Missing()
    {
        var controller = new MembershipController(new StubMembershipService
        {
            ExceptionToThrow = new KeyNotFoundException()
        });

        var result = await controller.RemoveUserFromOrganizationAsync(
            new RemoveUserRequest
            {
                InitiatorUserId = Guid.NewGuid().ToString(),
                UserId = Guid.NewGuid().ToString(),
                OrganizationId = Guid.NewGuid().ToString()
            },
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task RemoveUserFromOrganizationAsync_Returns_NoContent_When_Remove_Succeeds()
    {
        var service = new StubMembershipService();
        var controller = new MembershipController(service);
        var initiatorUserId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        var result = await controller.RemoveUserFromOrganizationAsync(
            new RemoveUserRequest
            {
                InitiatorUserId = initiatorUserId.ToString(),
                UserId = userId.ToString(),
                OrganizationId = organizationId.ToString()
            },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<NoContentResult>());
            Assert.That(service.LastInitiatorUserId, Is.EqualTo(initiatorUserId));
            Assert.That(service.LastUserId, Is.EqualTo(userId));
            Assert.That(service.LastOrganizationId, Is.EqualTo(organizationId));
        });
    }

    [Test]
    public async Task LeaveOrganizationAsync_Returns_NoContent_When_Leave_Succeeds()
    {
        var service = new StubMembershipService();
        var controller = new MembershipController(service);
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        var result = await controller.LeaveOrganizationAsync(
            new RemoveMembershipRequest
            {
                UserId = userId.ToString(),
                OrganizationId = organizationId.ToString()
            },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<NoContentResult>());
            Assert.That(service.LastUserId, Is.EqualTo(userId));
            Assert.That(service.LastOrganizationId, Is.EqualTo(organizationId));
        });
    }

    private sealed class StubMembershipService : IMembershipService
    {
        public Exception? ExceptionToThrow { get; init; }
        public Guid? LastInitiatorUserId { get; private set; }
        public Guid? LastUserId { get; private set; }
        public Guid? LastOrganizationId { get; private set; }

        public Task LeaveOrganizationAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            LastUserId = userId;
            LastOrganizationId = organizationId;
            return Task.CompletedTask;
        }

        public Task RemoveUserFromOrganizationAsync(Guid initiatorUserId, Guid userId, Guid organizationId, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            LastInitiatorUserId = initiatorUserId;
            LastUserId = userId;
            LastOrganizationId = organizationId;
            return Task.CompletedTask;
        }
    }
}
