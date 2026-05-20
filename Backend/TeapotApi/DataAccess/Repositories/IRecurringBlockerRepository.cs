using DataAccess.Models;

namespace DataAccess.Repositories;

public interface IRecurringBlockerRepository
{
    Task<IReadOnlyList<RecurringBlocker>> GetByWorkProfileAsync(Guid workProfileId, CancellationToken cancellationToken = default);
    Task<RecurringBlocker?> GetByIdAsync(Guid workProfileId, Guid blockerId, CancellationToken cancellationToken = default);
    Task AddAsync(RecurringBlocker blocker, CancellationToken cancellationToken = default);
    Task UpdateAsync(RecurringBlocker blocker, CancellationToken cancellationToken = default);
    Task DeleteAsync(RecurringBlocker blocker, CancellationToken cancellationToken = default);
}
