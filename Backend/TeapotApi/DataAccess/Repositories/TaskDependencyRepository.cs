using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class TaskDependencyRepository(TeapotDbContext context) : ITaskDependencyRepository
{
    public async Task<IReadOnlyList<TaskDependency>> GetByWorkProfileAsync(
        Guid workProfileId, CancellationToken cancellationToken = default)
    {
        var taskIds = await context.UserTasks
            .Where(t => t.WorkProfileId == workProfileId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        return await context.TaskDependencies
            .Where(d => taskIds.Contains(d.TaskId))
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceForTaskAsync(
        Guid taskId, IEnumerable<Guid> dependsOnIds, CancellationToken cancellationToken = default)
    {
        var existing = await context.TaskDependencies
            .Where(d => d.TaskId == taskId)
            .ToListAsync(cancellationToken);

        context.TaskDependencies.RemoveRange(existing);

        foreach (var depId in dependsOnIds)
            context.TaskDependencies.Add(new TaskDependency { TaskId = taskId, DependsOnTaskId = depId });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAllReferencesAsync(
        Guid taskId, CancellationToken cancellationToken = default)
    {
        // Remove rows where this task is the dependent AND where it is the predecessor,
        // so a task can be deleted even when other tasks still depend on it.
        var refs = await context.TaskDependencies
            .Where(d => d.TaskId == taskId || d.DependsOnTaskId == taskId)
            .ToListAsync(cancellationToken);

        if (refs.Count == 0) return;

        context.TaskDependencies.RemoveRange(refs);
        await context.SaveChangesAsync(cancellationToken);
    }
}
