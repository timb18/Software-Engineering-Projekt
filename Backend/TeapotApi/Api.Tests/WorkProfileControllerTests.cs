using Api.Controller;
using DataAccess.Models;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests;

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

    [Test]
    public async Task Put_Maps_Request_Without_Requiring_Entity_Navigation_Properties()
    {
        var service = new StubWorkProfileService();
        var controller = new WorkProfileController(service);
        var userId = Guid.NewGuid();
        var request = new WorkProfileSaveRequest(
            "08:00:00",
            "07:00",
            "21:00",
            [
                new WorkDayProfileRequest(
                    "Mon",
                    [
                        new WorkBlockRequest(Guid.NewGuid(), "org-1", "Org 1", "09:00", "17:00")
                    ],
                    [
                        new WorkBreakRequest(Guid.NewGuid(), "12:00", "12:30")
                    ])
            ]);

        var result = await controller.Put(userId, null, request, CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That(service.LastSavedProfile, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(service.LastSavedProfile!.PlannerViewStart, Is.EqualTo("07:00"));
            Assert.That(service.LastSavedProfile.PlannerViewEnd, Is.EqualTo("21:00"));
            Assert.That(service.LastSavedProfile.MaxDailyLoad, Is.EqualTo(TimeSpan.FromHours(8)));
            Assert.That(service.LastSavedProfile.Days.Single().Blocks.Single().CompanyName, Is.EqualTo("Org 1"));
            Assert.That(service.LastSavedProfile.Days.Single().Breaks.Single().StartTime, Is.EqualTo("12:00"));
        });
    }

    private sealed class StubWorkProfileService : IWorkProfileService
    {
        public Exception? ExceptionToThrow { get; init; }
        public Guid? LastDeletedUserId { get; private set; }
        public WorkProfile? LastSavedProfile { get; private set; }

        public Task<WorkProfile?> GetAsync(Guid userId, Guid? organizationId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkProfile?>(null);

        public Task<WorkProfile> SaveAsync(Guid userId, WorkProfile profile, Guid? organizationId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(LastSavedProfile = profile);

        public Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            LastDeletedUserId = userId;
            return Task.CompletedTask;
        }

        public Task DeleteByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.CompletedTask;
        }
    }
}
