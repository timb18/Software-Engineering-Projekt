using DataAccess.Models;

namespace DataAccess.Repositories;

public interface IUserTaskRepository
{
    Task<IEnumerable<UserTask>> GetByWorkProfileAsync(Guid workProfileId, CancellationToken cancellationToken = default);
    Task<UserTask?> FindAsync(Guid taskId, Guid workProfileId, CancellationToken cancellationToken = default);
    Task AddAsync(UserTask task, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserTask task, CancellationToken cancellationToken = default);
    Task DeleteAsync(UserTask task, CancellationToken cancellationToken = default);
}
