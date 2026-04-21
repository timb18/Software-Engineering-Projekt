using Model;

namespace DataAccess;

public interface IWorkProfileRepository
{
    /// <summary>Returns the work profile for the given user, or null if none exists yet.</summary>
    Task<WorkProfile?> GetByUserIdAsync(string userId);

    /// <summary>
    /// Creates or fully replaces the work profile for a user.
    /// Returns the saved profile.
    /// </summary>
    Task<WorkProfile> UpsertAsync(WorkProfile profile);
}
