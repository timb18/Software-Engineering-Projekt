using Api.Controller;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests;

[TestFixture]
public class OrganizationControllerTests
{
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
        public DeleteOrganizationCommand? LastCommand { get; private set; }

        public Task<IEnumerable<OrganizationDetailsDto>> GetOrganizationsForUserAsync(string email) =>
            Task.FromResult<IEnumerable<OrganizationDetailsDto>>([]);

        public Task DeleteOrganizationAsync(DeleteOrganizationCommand command, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            LastCommand = command;
            return Task.CompletedTask;
        }
    }

    private sealed class StubOrganizationAdminService : IOrganizationAdminService
    {
        public Task<CreateOrganizationResult> CreateOrganizationAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CreateOrganizationResult());
    }
}
