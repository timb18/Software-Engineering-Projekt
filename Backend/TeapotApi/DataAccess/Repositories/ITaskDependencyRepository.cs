using DataAccess.Models;

namespace DataAccess.Repositories;

public interface ITaskDependencyRepository
{
    Task<IReadOnlyList<TaskDependency>> GetByWorkProfileAsync(
        Guid workProfileId, CancellationToken cancellationToken = default);
}
