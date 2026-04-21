using Model;

namespace Services;

public interface IWorkProfileService
{
    /// <summary>Returns the work profile for the given user, or null if none exists.</summary>
    Task<WorkProfile?> GetAsync(string userId);

    /// <summary>Saves (creates or replaces) a work profile. Returns the saved profile.</summary>
    Task<WorkProfile> SaveAsync(WorkProfile profile);
}
