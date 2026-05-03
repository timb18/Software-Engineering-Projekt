using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class TaskDependencyRepository(TeapotDbContext context): ITaskDependencyRepository
{
    private readonly DbSet<TaskDependency> _dbSet = context.Set<TaskDependency>();
    public async Task<IEnumerable<TaskDependency>> GetTaskDependeciesForUserTasks(List<UserTask> userTasks, CancellationToken cancellationToken = default)
    {
        return _dbSet.Where(td => userTasks.Select(ut => ut.Id).Contains(td.TaskId)).ToList();
    }
}