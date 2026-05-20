using DataAccess.Models;

namespace DataAccess.Repositories;

/// <summary>
///     Data access interface for Membership entity operations.
///     Handles user-organization relationships and membership role management.
/// </summary>
/// <remarks>
///     A membership represents a user's participation in an organization with an assigned role.
///     This repository manages all membership lifecycle operations.
/// </remarks>
public interface IMembershipRepository
{
    /// <summary>
    ///     Finds a membership for a user in a specific organization.
    /// </summary>
    /// <param name="userId">The user GUID</param>
    /// <param name="organizationId">The organization GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The membership if found, null otherwise</returns>
    Task<Membership?> FindAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Finds a membership with the associated work profile loaded.
    /// </summary>
    /// <param name="userId">The user GUID</param>
    /// <param name="organizationId">The organization GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The membership with WorkProfile loaded, null if not found</returns>
    /// <remarks>Used when needing to access the user's schedule and tasks in an organization</remarks>
    Task<Membership?> FindWithWorkProfileAsync(Guid userId, Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Finds an organizer (user with Organizer role) in a specific organization.
    /// </summary>
    /// <param name="organizationId">The organization GUID</param>
    /// <param name="userId">The user GUID to check for organizer status</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The membership if user is an organizer, null otherwise</returns>
    /// <remarks>Used for permission checking on organization admin operations</remarks>
    Task<Membership?> FindOrganizerAsync(Guid organizationId, Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a user (by email) is a member of an organization.
    /// </summary>
    /// <param name="organizationId">The organization GUID</param>
    /// <param name="normalizedEmail">The user's email address</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>True if the email belongs to an organization member, false otherwise</returns>
    Task<bool> IsMemberByEmailAsync(Guid organizationId, string normalizedEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Finds a user's personal workspace membership (MaxUsers = 1).
    /// </summary>
    /// <param name="userId">The user GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The personal workspace membership if found, null otherwise</returns>
    /// <remarks>Every user has a personal workspace auto-created on first login</remarks>
    Task<Membership?> FindPersonalAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Counts the number of organizers in an organization.
    /// </summary>
    /// <param name="organizationId">The organization GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The count of members with Organizer role</returns>
    /// <remarks>Used to prevent organizations from losing all organizers</remarks>
    Task<int> CountOrganizersAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a new membership and persists it to the database.
    /// </summary>
    /// <param name="membership">The new membership entity</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    Task AddAsync(Membership membership, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves changes to existing memberships (e.g., role changes).
    /// </summary>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a membership and all associated work profile data (tasks, schedules, blocks).
    /// </summary>
    /// <param name="membership">The membership to delete</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <remarks>Cascades deletion to all work profile tasks and scheduling information</remarks>
    Task DeleteWithWorkProfileDataAsync(Membership membership, CancellationToken cancellationToken = default);
}