using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class UserTaskService(IGenericRepository<UserTask> repository) : IUserTaskService
{
    public async Task<IEnumerable<UserTask>> GetTasksAsync(
        Guid workProfileId, CancellationToken cancellationToken = default)
    {
        return await repository.GetQueryable()
            .Where(t => t.WorkProfileId == workProfileId)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserTask> CreateTaskAsync(
        Guid workProfileId, UserTask task, CancellationToken cancellationToken = default)
    {
        // Reset fields that must be server-assigned, regardless of what the client sent
        task.Id = Guid.Empty;
        task.WorkProfileId = workProfileId;
        task.WorkProfile = null;
        task.CreatedAt = DateTime.UtcNow;
        task.EditedAt = null;
        await repository.AddAsync(task, cancellationToken);
        return task;
    }

    public async Task<UserTask> UpdateTaskAsync(
        Guid workProfileId, Guid taskId, UserTask updated, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == taskId && t.WorkProfileId == workProfileId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task {taskId} not found.");

        existing.Name = updated.Name;
        existing.Description = updated.Description;
        existing.Priority = updated.Priority;
        existing.IsFixed = updated.IsFixed;
        existing.TimeEstimate = updated.TimeEstimate;
        existing.Deadline = updated.Deadline;
        existing.Status = updated.Status;
        existing.EarlyStart = updated.EarlyStart;
        existing.EarlyFinish = updated.EarlyFinish;
        existing.LateStart = updated.LateStart;
        existing.LateFinish = updated.LateFinish;
        existing.Intensity = updated.Intensity;
        existing.EditedAt = DateTime.UtcNow;

        await repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task DeleteTaskAsync(
        Guid workProfileId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await repository.GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == taskId && t.WorkProfileId == workProfileId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task {taskId} not found.");

        await repository.DeleteAsync(task, cancellationToken);
    }
}