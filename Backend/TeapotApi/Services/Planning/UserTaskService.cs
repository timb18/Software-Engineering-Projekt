using DataAccess.Models;
using DataAccess.Repositories;

namespace Services.Planning;

public class UserTaskService(IUserTaskRepository userTaskRepository, ITaskDependencyRepository taskDependencyRepository, ITaskBlockRepository taskBlockRepository) : IUserTaskService
{
    public async Task<IEnumerable<UserTask>> GetTasksAsync(
        Guid workProfileId, CancellationToken cancellationToken = default)
    {
        var tasks = (await userTaskRepository.GetByWorkProfileAsync(workProfileId, cancellationToken)).ToList();
        var dependencies = await taskDependencyRepository.GetByWorkProfileAsync(workProfileId, cancellationToken);

        var depsByTaskId = dependencies
            .GroupBy(d => d.TaskId)
            .ToDictionary(g => g.Key, g => g.Select(d => d.DependsOnTaskId).ToList());

        foreach (var task in tasks)
            task.DependsOnTaskIds = depsByTaskId.TryGetValue(task.Id, out var ids) ? ids : [];

        return tasks;
    }

    public async Task<UserTask> GetTaskAsync(Guid workProfileId, Guid taskId, CancellationToken cancellationToken = default)
    {
        return await userTaskRepository.FindAsync(taskId, workProfileId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task {taskId} not found.");
    }

    public async Task<UserTask> CreateTaskAsync(
        Guid workProfileId, UserTask task, CancellationToken cancellationToken = default)
    {
        var dependsOnIds = task.DependsOnTaskIds.ToList();
        task.Id = Guid.Empty;
        task.WorkProfileId = workProfileId;
        task.WorkProfile = null;
        task.CreatedAt = DateTime.UtcNow;
        task.EditedAt = null;
        await userTaskRepository.AddAsync(task, cancellationToken);
        if (dependsOnIds.Count > 0)
            await taskDependencyRepository.ReplaceForTaskAsync(task.Id, dependsOnIds, cancellationToken);
        if (task.IsFixed)
            await taskBlockRepository.UpsertFixedBlockAsync(task.Id, task.EarlyStart, task.EarlyFinish, cancellationToken);
        return task;
    }

    public async Task<UserTask> UpdateTaskAsync(
        Guid workProfileId, Guid taskId, UserTask updated, CancellationToken cancellationToken = default)
    {
        var existing = await userTaskRepository.FindAsync(taskId, workProfileId, cancellationToken)
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

        await userTaskRepository.UpdateAsync(existing, cancellationToken);
        await taskDependencyRepository.ReplaceForTaskAsync(taskId, updated.DependsOnTaskIds, cancellationToken);
        if (existing.IsFixed)
            await taskBlockRepository.UpsertFixedBlockAsync(taskId, existing.EarlyStart, existing.EarlyFinish, cancellationToken);
        else
            await taskBlockRepository.DeleteForTaskAsync(taskId, cancellationToken);
        existing.DependsOnTaskIds = updated.DependsOnTaskIds;
        return existing;
    }

    public async Task DeleteTaskAsync(
        Guid workProfileId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await userTaskRepository.FindAsync(taskId, workProfileId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task {taskId} not found.");

        await taskBlockRepository.DeleteForTaskAsync(taskId, cancellationToken);
        await taskDependencyRepository.ReplaceForTaskAsync(taskId, [], cancellationToken);
        await userTaskRepository.DeleteAsync(task, cancellationToken);
    }
}
