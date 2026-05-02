using DataAccess.Models;

namespace DataAccess.Repositories;

public interface ITaskBlockRepository
{
    Task<IEnumerable<TaskBlock>> GetFixedTaskBlocksForTaskIdsAsync(List<Guid> taskIds, CancellationToken cancellationToken);
}