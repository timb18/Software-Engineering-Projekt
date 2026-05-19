using DataAccess.Models;

namespace DataAccess.Repositories;

/// <summary>
/// Data access interface for WorkProfile entity operations.
/// Manages user work schedules, daily work configurations, and time intervals.
/// </summary>
/// <remarks>
/// WorkProfiles define how much time a user is available per day and when.
/// Each user has one WorkProfile per organization membership.
/// </remarks>
public interface IWorkProfileRepository
{
    /// <summary>
    /// Gets the personal workspace profile for a user.
    /// </summary>
    /// <param name="userId">The user GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The work profile for the user's personal workspace, null if not created</returns>
    /// <remarks>Every user has a personal workspace auto-created on first login</remarks>
    Task<WorkProfile?> GetPersonalAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the personal workspace profile without tracking changes (read-only).
    /// </summary>
    /// <param name="userId">The user GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The work profile for the user's personal workspace (not tracked), null if not created</returns>
    /// <remarks>Used for read-only queries to avoid EF Core change tracking overhead</remarks>
    Task<WorkProfile?> GetPersonalNoTrackingAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Finds a work profile by the user's ID (searches the user's personal workspace).
    /// </summary>
    /// <param name="userId">The user GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The user's personal workspace profile, null if not found</returns>
    Task<WorkProfile?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Finds a work profile by its unique identifier.
    /// </summary>
    /// <param name="workProfileId">The work profile GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The work profile if found, null otherwise</returns>
    Task<WorkProfile?> GetByIdAsync(Guid workProfileId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all time intervals during which a work profile is active.
    /// </summary>
    /// <param name="workProfileId">The work profile GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>List of active time intervals (e.g., for seasonal schedules)</returns>
    /// <remarks>Used by the scheduling algorithm to determine when to schedule tasks</remarks>
    Task<IReadOnlyList<WorkProfileTimeInterval>> GetTimeIntervalsAsync(Guid workProfileId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a work profile by user ID with all related data loaded for deletion.
    /// </summary>
    /// <param name="userId">The user GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The work profile with all relations loaded, null if not found</returns>
    /// <remarks>Used when preparing a work profile for complete deletion with cascading data</remarks>
    Task<WorkProfile?> GetForDeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a work profile by user email with all related data loaded for deletion.
    /// </summary>
    /// <param name="normalizedEmail">The user's email address</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The personal work profile with all relations loaded, null if not found</returns>
    /// <remarks>Used when deleting a user account and their personal workspace</remarks>
    Task<WorkProfile?> GetForDeleteByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new work profile and persists it to the database.
    /// </summary>
    /// <param name="profile">The new work profile entity</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    Task AddAsync(WorkProfile profile, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Replaces the work day profiles (daily schedules) for a work profile.
    /// </summary>
    /// <param name="workProfileId">The work profile whose schedule is being replaced</param>
    /// <param name="oldDays">The existing work day profiles to remove</param>
    /// <param name="newDays">The new work day profiles to add</param>
    /// <param name="deleteFlexibleTaskBlocks">Whether generated, non-fixed task blocks must be cleared in the same transaction</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <remarks>Used when a user updates their work schedule (e.g., changing work hours)</remarks>
    Task ReplaceDaysAsync(
        Guid workProfileId,
        IList<WorkDayProfile> oldDays,
        IList<WorkDayProfile> newDays,
        bool deleteFlexibleTaskBlocks = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a work profile and all associated data (tasks, schedules, blocks).
    /// </summary>
    /// <param name="profile">The work profile to delete</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <remarks>Cascades deletion to all tasks and scheduling information</remarks>
    Task DeleteAsync(WorkProfile profile, CancellationToken cancellationToken = default);
}
