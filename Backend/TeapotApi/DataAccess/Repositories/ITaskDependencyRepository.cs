using DataAccess.Models;

namespace DataAccess.Repositories;

public interface ITaskDependencyRepository
{
    Task<IReadOnlyList<TaskDependency>> GetByWorkProfileAsync(
        Guid workProfileId, CancellationToken cancellationToken = default);

    Task ReplaceForTaskAsync(
        Guid taskId, IEnumerable<Guid> dependsOnIds, CancellationToken cancellationToken = default);
}
