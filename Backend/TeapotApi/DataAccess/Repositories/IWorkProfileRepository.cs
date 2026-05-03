using DataAccess.Models;

namespace DataAccess.Repositories;

public interface IWorkProfileRepository
{
    Task<WorkProfile?> GetPersonalAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<WorkProfile?> GetPersonalNoTrackingAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<WorkProfile?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<WorkProfile?> GetForDeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<WorkProfile?> GetForDeleteByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task AddAsync(WorkProfile profile, CancellationToken cancellationToken = default);
    Task ReplaceDaysAsync(IList<WorkDayProfile> oldDays, IList<WorkDayProfile> newDays, CancellationToken cancellationToken = default);
    Task DeleteAsync(WorkProfile profile, CancellationToken cancellationToken = default);
    Task<WorkProfile?> GetWorkProfileWithWorkDayProfileByIdAsync(Guid workProfileId, CancellationToken cancellationToken = default);
}
