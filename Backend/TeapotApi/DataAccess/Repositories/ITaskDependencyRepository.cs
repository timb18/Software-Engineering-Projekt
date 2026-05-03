using DataAccess.Models;

namespace DataAccess.Repositories;

public interface ITaskDependencyRepository
{
    Task<IEnumerable<TaskDependency>> GetTaskDependeciesForUserTasks(List<UserTask> userTasks, CancellationToken cancellationToken);
}