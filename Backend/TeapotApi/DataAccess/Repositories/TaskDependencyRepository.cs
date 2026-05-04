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
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM task_dependencies WHERE task_id = {taskId}", cancellationToken);

        foreach (var depId in dependsOnIds)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO task_dependencies (task_id, depends_on_task_id) VALUES ({taskId}, {depId})",
                cancellationToken);
        }
    }
}
