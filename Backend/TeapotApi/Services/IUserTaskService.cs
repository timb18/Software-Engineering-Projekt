using DataAccess.Models;

namespace Services;

public interface IUserTaskService
{
    Task<IEnumerable<UserTask>> GetTasksAsync(Guid workProfileId, CancellationToken cancellationToken = default);
    Task<UserTask> CreateTaskAsync(Guid workProfileId, UserTask task, CancellationToken cancellationToken = default);
    Task<UserTask> UpdateTaskAsync(Guid workProfileId, Guid taskId, UserTask task, CancellationToken cancellationToken = default);
    Task DeleteTaskAsync(Guid workProfileId, Guid taskId, CancellationToken cancellationToken = default);
}