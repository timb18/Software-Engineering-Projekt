using Api.Controller;
using DataAccess.Models;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace DataAccessTests;

[TestFixture]
public class WorkProfileControllerTests
{
    [Test]
    public async Task Delete_Returns_NoContent_When_Delete_Succeeds()
    {
        var service = new StubWorkProfileService();
        var controller = new WorkProfileController(service);
        var userId = Guid.NewGuid();

        var result = await controller.Delete(userId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<NoContentResult>());
            Assert.That(service.LastDeletedUserId, Is.EqualTo(userId));
        });
    }

    [Test]
    public async Task Delete_Returns_NotFound_When_Profile_Is_Missing()
    {
        var controller = new WorkProfileController(new StubWorkProfileService
        {
            ExceptionToThrow = new KeyNotFoundException()
        });

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    private sealed class StubWorkProfileService : IWorkProfileService
    {
        public Exception? ExceptionToThrow { get; init; }
        public Guid? LastDeletedUserId { get; private set; }

        public Task<WorkProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkProfile?>(null);

        public Task<WorkProfile> SaveAsync(Guid userId, WorkProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(profile);

        public Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            LastDeletedUserId = userId;
            return Task.CompletedTask;
        }
    }
}
