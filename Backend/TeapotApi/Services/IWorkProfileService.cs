using DataAccess.Models;

namespace Services;

public interface IWorkProfileService
{
    /// <summary>Returns the work profile for the given user, or null if none exists.</summary>
    Task<WorkProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Saves (creates or replaces) a work profile. Returns the saved profile.</summary>
    Task<WorkProfile> SaveAsync(WorkProfile profile, CancellationToken cancellationToken = default);
}