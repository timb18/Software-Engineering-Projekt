using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Services.Tests;

[TestFixture]
public class UserTaskServiceTests
{
    private TeapotDbContext _dbContext = null!;
    private UserTaskService _service = null!;
    private Guid _workProfileId;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<TeapotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TeapotDbContext(options);

        // Seed the minimal entities required by foreign keys
        var user = new User { Email = "task-test@example.com", CreatedAt = DateTime.UtcNow };
        var org = new Organization { Name = "Test Org", Description = "desc", MaxUsers = 5, CreatedAt = DateTime.UtcNow };
        _dbContext.AddRange(user, org);
        _dbContext.SaveChanges();

        var membership = new Membership
        {
            UserId = user.Id,
            OrganizationId = org.Id,
            Role = ERole.User,
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.Add(membership);
        _dbContext.SaveChanges();

        var workProfile = new WorkProfile { MembershipId = membership.Id, CreatedAt = DateTime.UtcNow };
        _dbContext.Add(workProfile);
        _dbContext.SaveChanges();

        _workProfileId = workProfile.Id;
        _service = new UserTaskService(new UserTaskRepository(_dbContext), new TaskDependencyRepository(_dbContext));
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private UserTask MakeTask(string name = "Task", string status = "todo") => new()
    {
        WorkProfileId = _workProfileId,
        Name = name,
        Description = "desc",
        Priority = ETaskPriority.Medium,
        Intensity = ETaskIntensity.Normal,
        TimeEstimate = TimeSpan.FromHours(1),
        Status = status,
        EarlyStart = DateTime.UtcNow,
        EarlyFinish = DateTime.UtcNow.AddHours(1),
        LateStart = DateTime.UtcNow,
        LateFinish = DateTime.UtcNow.AddHours(1),
    };

    // ── GetTasksAsync ─────────────────────────────────────────────────────────

    [Test]
    public async Task GetTasksAsync_Returns_Tasks_Belonging_To_WorkProfile()
    {
        await _service.CreateTaskAsync(_workProfileId, MakeTask("A"));
        await _service.CreateTaskAsync(_workProfileId, MakeTask("B"));

        var result = (await _service.GetTasksAsync(_workProfileId)).ToList();

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(t => t.WorkProfileId == _workProfileId), Is.True);
    }

    [Test]
    public async Task GetTasksAsync_Excludes_Tasks_From_Other_WorkProfiles()
    {
        await _service.CreateTaskAsync(_workProfileId, MakeTask("mine"));

        // Insert a task for a different work profile directly (no FK check in InMemory)
        var otherId = Guid.NewGuid();
        var foreign = MakeTask("foreign");
        foreign.WorkProfileId = otherId;
        _dbContext.Set<UserTask>().Add(foreign);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetTasksAsync(_workProfileId);

        Assert.That(result.Select(t => t.Name), Does.Not.Contain("foreign"));
    }

    // ── CreateTaskAsync ───────────────────────────────────────────────────────

    [Test]
    public async Task CreateTaskAsync_Assigns_New_Id_Ignoring_Client_Value()
    {
        var task = MakeTask();
        var clientId = Guid.NewGuid();
        task.Id = clientId;

        var result = await _service.CreateTaskAsync(_workProfileId, task);

        Assert.That(result.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result.Id, Is.Not.EqualTo(clientId));
    }

    [Test]
    public async Task CreateTaskAsync_Sets_WorkProfileId_On_The_Entity()
    {
        var result = await _service.CreateTaskAsync(_workProfileId, MakeTask());

        Assert.That(result.WorkProfileId, Is.EqualTo(_workProfileId));
    }

    [Test]
    public async Task CreateTaskAsync_Clears_EditedAt()
    {
        var task = MakeTask();
        task.EditedAt = DateTime.UtcNow.AddDays(-1);

        var result = await _service.CreateTaskAsync(_workProfileId, task);

        Assert.That(result.EditedAt, Is.Null);
    }

    [Test]
    public async Task CreateTaskAsync_Sets_CreatedAt_To_UtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await _service.CreateTaskAsync(_workProfileId, MakeTask());

        Assert.That(result.CreatedAt, Is.GreaterThan(before));
    }

    // ── UpdateTaskAsync ───────────────────────────────────────────────────────

    [Test]
    public async Task UpdateTaskAsync_Updates_Name_And_Status()
    {
        var task = await _service.CreateTaskAsync(_workProfileId, MakeTask("Original", "todo"));

        var updated = MakeTask("Renamed", "done");
        var result = await _service.UpdateTaskAsync(_workProfileId, task.Id, updated);

        Assert.That(result.Name, Is.EqualTo("Renamed"));
        Assert.That(result.Status, Is.EqualTo("done"));
    }

    [Test]
    public async Task UpdateTaskAsync_Sets_EditedAt()
    {
        var task = await _service.CreateTaskAsync(_workProfileId, MakeTask());

        var result = await _service.UpdateTaskAsync(_workProfileId, task.Id, MakeTask("changed"));

        Assert.That(result.EditedAt, Is.Not.Null);
    }

    [Test]
    public void UpdateTaskAsync_Throws_KeyNotFoundException_When_Task_Does_Not_Exist()
    {
        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _service.UpdateTaskAsync(_workProfileId, Guid.NewGuid(), MakeTask()));
    }

    [Test]
    public async Task UpdateTaskAsync_Throws_KeyNotFoundException_For_Wrong_WorkProfile()
    {
        var task = await _service.CreateTaskAsync(_workProfileId, MakeTask());

        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _service.UpdateTaskAsync(Guid.NewGuid(), task.Id, MakeTask()));
    }

    // ── DeleteTaskAsync ───────────────────────────────────────────────────────

    [Test]
    public async Task DeleteTaskAsync_Removes_The_Task()
    {
        var task = await _service.CreateTaskAsync(_workProfileId, MakeTask());

        await _service.DeleteTaskAsync(_workProfileId, task.Id);

        var remaining = await _service.GetTasksAsync(_workProfileId);
        Assert.That(remaining, Is.Empty);
    }

    [Test]
    public void DeleteTaskAsync_Throws_KeyNotFoundException_When_Task_Does_Not_Exist()
    {
        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _service.DeleteTaskAsync(_workProfileId, Guid.NewGuid()));
    }
}
