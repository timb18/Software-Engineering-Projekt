using DataAccess.Models;

namespace DataAccess.Repositories;

public interface IWorkProfileRepository: IGenericRepository<WorkProfile>
{
    Task<WorkProfile> GetWorkProfileWithWorkDayProfileByIdAsync(Guid workProfileId, CancellationToken cancellationToken = default);
}