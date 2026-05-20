using DataAccess.Models;
using DataAccess.Repositories;

namespace Services.Planning;

/// <summary>
///     Implements CRUD operations for user tasks and keeps dependencies and fixed blocks in sync.
/// </summary>
public class UserTaskService(
    IUserTaskRepository userTaskRepository,
    ITaskDependencyRepository taskDependencyRepository,
    ITaskBlockRepository taskBlockRepository) : IUserTaskService
{
    /// <summary>
    ///     Loads all tasks for a work profile and attaches dependency ids to each task.
    /// </summary>
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

    /// <summary>
    ///     Loads a single task for the given work profile.
    /// </summary>
    public async Task<UserTask> GetTaskAsync(Guid workProfileId, Guid taskId,
        CancellationToken cancellationToken = default)
    {
        return await userTaskRepository.FindAsync(taskId, workProfileId, cancellationToken)
               ?? throw new KeyNotFoundException($"Task {taskId} not found.");
    }

    /// <summary>
    ///     Creates a new task, stores dependencies, and persists fixed task blocks when needed.
    /// </summary>
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
            await taskBlockRepository.UpsertFixedBlockAsync(task.Id, task.EarlyStart, task.EarlyFinish,
                cancellationToken);
        return task;
    }

    /// <summary>
    ///     Updates a task and synchronizes its dependencies and fixed blocks.
    /// </summary>
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
        existing.OrganizationId = updated.OrganizationId;
        existing.EditedAt = DateTime.UtcNow;

        await userTaskRepository.UpdateAsync(existing, cancellationToken);
        await taskDependencyRepository.ReplaceForTaskAsync(taskId, updated.DependsOnTaskIds, cancellationToken);
        if (existing.IsFixed)
            await taskBlockRepository.UpsertFixedBlockAsync(taskId, existing.EarlyStart, existing.EarlyFinish,
                cancellationToken);
        else
            await taskBlockRepository.DeleteForTaskAsync(taskId, cancellationToken);
        existing.DependsOnTaskIds = updated.DependsOnTaskIds;
        return existing;
    }

    /// <summary>
    ///     Deletes a task and removes any dependent scheduling data first.
    /// </summary>
    public async Task DeleteTaskAsync(
        Guid workProfileId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await userTaskRepository.FindAsync(taskId, workProfileId, cancellationToken)
                   ?? throw new KeyNotFoundException($"Task {taskId} not found.");

        await taskBlockRepository.DeleteForTaskAsync(taskId, cancellationToken);
        // Wipe every dependency row that mentions this task – both directions – so the
        // FK constraint does not block the delete when other tasks still depend on it.
        await taskDependencyRepository.DeleteAllReferencesAsync(taskId, cancellationToken);
        await userTaskRepository.DeleteAsync(task, cancellationToken);
    }
}