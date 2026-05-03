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
}
