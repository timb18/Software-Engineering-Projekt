using DataAccess.Models;

namespace Services.WorkProfiles;

/// <summary>
///     Work-profile management operations used by the planning subsystem.
/// </summary>
public interface IWorkProfileService
{
    /// <summary>Returns the work profile for the given user, or null if none exists.</summary>
    Task<WorkProfile?> GetAsync(Guid userId, Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Saves (creates or replaces) a work profile for the given user. Returns the saved profile.</summary>
    Task<WorkProfile> SaveAsync(Guid userId, WorkProfile profile, Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the work profile and dependent planning data for the given user.</summary>
    Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the work profile and dependent planning data for the given user email.</summary>
    Task DeleteByEmailAsync(string email, CancellationToken cancellationToken = default);
}