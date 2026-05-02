using DataAccess.Models;

namespace DataAccess.Repositories;

public interface ITaskDependencyRepository: IGenericRepository<TaskDependency>
{
    Task<IEnumerable<TaskDependency>> GetTaskDependeciesForUserTasks(List<UserTask> userTasks, CancellationToken cancellationToken);
}