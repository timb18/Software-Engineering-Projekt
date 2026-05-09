using Api.Controller;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests;

[TestFixture]
public class OrganizationControllerTests
{
    [Test]
    public async Task GetByUserEmail_Returns_Ok_With_Organizations()
    {
        var organizations = new[]
        {
            new OrganizationDetailsDto { Id = Guid.NewGuid(), Name = "Org A" },
            new OrganizationDetailsDto { Id = Guid.NewGuid(), Name = "Org B" }
        };

        var service = new StubOrganizationService { OrganizationsToReturn = organizations };
        var controller = new OrganizationController(new StubOrganizationAdminService(), service);

        var result = await controller.GetByUserEmail("user@example.com", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            Assert.That(service.LastGetByUserEmail, Is.EqualTo("user@example.com"));
            Assert.That(((OkObjectResult)result).Value, Is.EqualTo(organizations));
        });
    }

    [Test]
    public async Task GetByUserEmail_Returns_NotFound_When_Service_Throws_KeyNotFoundException()
    {
        var controller = new OrganizationController(
            new StubOrganizationAdminService(),
            new StubOrganizationService { ExceptionToThrow = new KeyNotFoundException("User not found") });

        var result = await controller.GetByUserEmail("missing@example.com", CancellationToken.None);

        Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task GetByUserEmail_Returns_BadRequest_When_Service_Throws_ArgumentException()
    {
        var controller = new OrganizationController(
            new StubOrganizationAdminService(),
            new StubOrganizationService { ExceptionToThrow = new ArgumentException("Email is required") });

        var result = await controller.GetByUserEmail("", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result).Value?.ToString(), Does.Contain("Email is required"));
        });
    }

    [Test]
    public async Task Delete_Returns_NoContent_When_Delete_Succeeds()
    {
        var service = new StubOrganizationService();
        var controller = new OrganizationController(new StubOrganizationAdminService(), service);
        var organizationId = Guid.NewGuid();
        var initiatorId = Guid.NewGuid();

        var result = await controller.Delete(
            organizationId,
            new DeleteOrganizationRequest(initiatorId, "Org"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<NoContentResult>());
            Assert.That(service.LastCommand, Is.Not.Null);
            Assert.That(service.LastCommand!.OrganizationId, Is.EqualTo(organizationId));
            Assert.That(service.LastCommand.InitiatorUserId, Is.EqualTo(initiatorId));
        });
    }

    [Test]
    public async Task Delete_Returns_Conflict_When_Organization_Has_Additional_Organizers()
    {
        var controller = new OrganizationController(
            new StubOrganizationAdminService(),
            new StubOrganizationService { ExceptionToThrow = new InvalidOperationException("Die Organisation kann nicht gelöscht werden, solange es weitere Organizer gibt.") });

        var result = await controller.Delete(
            Guid.NewGuid(),
            new DeleteOrganizationRequest(Guid.NewGuid(), "Org"),
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<ConflictObjectResult>());
    }

    [Test]
    public async Task Delete_Returns_Forbidden_When_User_Is_Not_Organizer()
    {
        var controller = new OrganizationController(
            new StubOrganizationAdminService(),
            new StubOrganizationService { ExceptionToThrow = new UnauthorizedAccessException("Only organizers can delete an organization.") });

        var result = await controller.Delete(
            Guid.NewGuid(),
            new DeleteOrganizationRequest(Guid.NewGuid(), "Org"),
            CancellationToken.None);

        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(403));
    }

    private sealed class StubOrganizationService : IOrganizationService
    {
        public Exception? ExceptionToThrow { get; init; }
        public IEnumerable<OrganizationDetailsDto> OrganizationsToReturn { get; init; } = [];
        public string? LastGetByUserEmail { get; private set; }
        public DeleteOrganizationCommand? LastCommand { get; private set; }
        public RenameOrganizationCommand? LastRenameCommand { get; private set; }

        public Task<IEnumerable<OrganizationDetailsDto>> GetOrganizationsForUserAsync(string email, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            LastGetByUserEmail = email;
            return Task.FromResult(OrganizationsToReturn);
        }

        public Task DeleteOrganizationAsync(DeleteOrganizationCommand command, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            LastCommand = command;
            return Task.CompletedTask;
        }

        public Task RenameOrganizationAsync(RenameOrganizationCommand command, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            LastRenameCommand = command;
            return Task.CompletedTask;
        }
    }

    private sealed class StubOrganizationAdminService : IOrganizationAdminService
    {
        public Task<CreateOrganizationResult> CreateOrganizationAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CreateOrganizationResult());
    }
}
