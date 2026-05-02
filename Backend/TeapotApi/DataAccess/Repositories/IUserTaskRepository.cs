using DataAccess.Models;

namespace DataAccess.Repositories;

public interface IUserTaskRepository : IGenericRepository<UserTask>
{
    Task<IEnumerable<UserTask>> GetTasksForWorkProfileIdAsync(Guid workProfileId,
        CancellationToken cancellationToken = default);

}