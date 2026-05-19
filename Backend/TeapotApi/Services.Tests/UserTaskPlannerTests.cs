using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Services.Tests;

[TestFixture]
public class UserTaskPlannerTests
{
    private TeapotDbContext _dbContext = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<TeapotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new TeapotDbContext(options);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    [Test]
    public async Task ScheduleAsync_StoresBerlinWorkProfileTimesAsUtcInstants()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var plannedLocalDay = today.AddDays(1);

        var user = new User
        {
            Email = "planner-timezone@example.com",
            Timezone = "Europe/Berlin",
            CreatedAt = DateTime.UtcNow
        };
        var organization = new Organization
        {
            Name = "Planner Org",
            Description = "desc",
            MaxUsers = 5,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.AddRange(user, organization);
        await _dbContext.SaveChangesAsync();

        var membership = new Membership
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Role = ERole.User,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Add(membership);
        await _dbContext.SaveChangesAsync();

        var workProfile = new WorkProfile
        {
            MembershipId = membership.Id,
            MaxDailyLoad = TimeSpan.FromHours(8),
            CreatedAt = DateTime.UtcNow,
            Days =
            [
                new WorkDayProfile
                {
                    Day = ToDayAbbreviation(plannedLocalDay.DayOfWeek),
                    Blocks =
                    [
                        new WorkBlock
                        {
                            CompanyId = organization.Id.ToString(),
                            CompanyName = organization.Name,
                            StartTime = "09:00",
                            EndTime = "17:00"
                        }
                    ]
                }
            ]
        };
        _dbContext.WorkProfiles.Add(workProfile);
        await _dbContext.SaveChangesAsync();

        var task = new UserTask
        {
            WorkProfileId = workProfile.Id,
            Name = "Timezone check",
            Description = "desc",
            Priority = ETaskPriority.Medium,
            Intensity = ETaskIntensity.Normal,
            TimeEstimate = TimeSpan.FromHours(1),
            Status = "todo",
            Deadline = TimeZoneInfo.ConvertTimeToUtc(plannedLocalDay.AddDays(2).ToDateTime(TimeOnly.MinValue), timeZone),
            CreatedAt = DateTime.UtcNow,
            EarlyStart = DateTime.UtcNow,
            EarlyFinish = DateTime.UtcNow.AddHours(1),
            LateStart = DateTime.UtcNow,
            LateFinish = DateTime.UtcNow.AddHours(1)
        };
        _dbContext.UserTasks.Add(task);
        await _dbContext.SaveChangesAsync();

        var planner = new UserTaskPlanner(
            new UserTaskRepository(_dbContext),
            new WorkProfileRepository(_dbContext),
            new TaskDependencyRepository(_dbContext),
            new InMemoryTaskBlockRepository(_dbContext),
            new DependencyAnalyzer(),
            new SchedulingAlgorithm());

        var result = await planner.ScheduleAsync(workProfile.Id);

        Assert.That(result.Success, Is.True, result.ErrorMessage);

        Assert.That(result.PlannedBlocks, Has.Count.EqualTo(1));
        var block = result.PlannedBlocks.Single();
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(block.StartDate, timeZone);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(block.EndDate, timeZone);

        Assert.That(block.StartDate.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(block.EndDate.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(DateOnly.FromDateTime(localStart), Is.EqualTo(plannedLocalDay));
        Assert.That(TimeOnly.FromDateTime(localStart), Is.EqualTo(new TimeOnly(9, 0)));
        Assert.That(TimeOnly.FromDateTime(localEnd), Is.EqualTo(new TimeOnly(10, 0)));
    }

    private static string ToDayAbbreviation(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Mon",
        DayOfWeek.Tuesday => "Tue",
        DayOfWeek.Wednesday => "Wed",
        DayOfWeek.Thursday => "Thu",
        DayOfWeek.Friday => "Fri",
        DayOfWeek.Saturday => "Sat",
        DayOfWeek.Sunday => "Sun",
        _ => throw new ArgumentOutOfRangeException(nameof(day))
    };

    private sealed class InMemoryTaskBlockRepository(TeapotDbContext context) : ITaskBlockRepository
    {
        public async Task<IReadOnlyList<TaskBlock>> GetByWorkProfileAsync(
            Guid workProfileId, CancellationToken cancellationToken = default)
        {
            var taskIds = await context.UserTasks
                .Where(t => t.WorkProfileId == workProfileId)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            return await context.TaskBlocks
                .Where(b => taskIds.Contains(b.TaskId))
                .Include(b => b.Task)
                .ToListAsync(cancellationToken);
        }

        public async Task ReplaceAsync(
            Guid workProfileId,
            IReadOnlyList<TaskBlock> newBlocks,
            CancellationToken cancellationToken = default)
        {
            var taskIds = await context.UserTasks
                .Where(t => t.WorkProfileId == workProfileId)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            var oldBlocks = await context.TaskBlocks
                .Where(b => taskIds.Contains(b.TaskId) && !b.IsFixed)
                .ToListAsync(cancellationToken);

            context.TaskBlocks.RemoveRange(oldBlocks);
            await context.TaskBlocks.AddRangeAsync(newBlocks, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        public Task DeleteForTaskAsync(Guid taskId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpsertFixedBlockAsync(Guid taskId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
