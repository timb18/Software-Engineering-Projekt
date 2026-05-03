using DataAccess.Models;

namespace Services.WorkProfiles;

public interface IWorkProfileService
{
    /// <summary>Returns the work profile for the given user, or null if none exists.</summary>
    Task<WorkProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Saves (creates or replaces) a work profile for the given user. Returns the saved profile.</summary>
    Task<WorkProfile> SaveAsync(Guid userId, WorkProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Deletes the work profile and dependent planning data for the given user.</summary>
    Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the work profile and dependent planning data for the given user email.</summary>
    Task DeleteByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Returns the work profile for the given profile ID, including days, blocks, and breaks. Returns null if not found.</summary>
    Task<WorkProfile?> GetByIdAsync(Guid workProfileId, CancellationToken cancellationToken = default);
}
