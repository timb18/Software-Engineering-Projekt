using DataAccess.Models;

namespace DataAccess.Repositories;

/// <summary>
/// Data access interface for Organization entity operations.
/// Provides methods for CRUD operations, finding organizations by various criteria, and cascade deletions.
/// </summary>
/// <remarks>
/// OrganizationRepository manages all database operations for organizations (teams/workspaces),
/// including loading related entities like members and invitations.
/// </remarks>
public interface IOrganizationRepository
{
    /// <summary>
    /// Finds an organization by its unique identifier.
    /// </summary>
    /// <param name="id">The organization GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The organization if found, null otherwise</returns>
    Task<Organization?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Finds an organization by its name.
    /// </summary>
    /// <param name="name">The organization name</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The organization if found, null otherwise</returns>
    Task<Organization?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Finds all organizations that a user is a member of.
    /// </summary>
    /// <param name="normalizedEmail">The user's email address</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>List of organizations where the user is a member, ordered by name</returns>
    /// <remarks>Loads all memberships, invitations, and work profiles for each organization</remarks>
    Task<IEnumerable<Organization>> GetForUserAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Finds an organization with all its memberships and invitations loaded.
    /// </summary>
    /// <param name="id">The organization GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The organization with all related entities loaded, null if not found</returns>
    /// <remarks>Used for operations that need full organization context (e.g., deletion, role changes)</remarks>
    Task<Organization?> GetWithMembershipsAndInvitationsAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new organization and persists it to the database.
    /// </summary>
    /// <param name="organization">The new organization entity</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    Task AddAsync(Organization organization, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Saves any changes to existing organizations.
    /// </summary>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <remarks>Used when modifying organization properties like name or description</remarks>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes an organization and all related cascading data (members, tasks, schedules, etc.).
    /// </summary>
    /// <param name="organization">The organization entity to delete (must have relationships loaded)</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <remarks>
    /// Cascading deletion order:
    /// 1. Task dependencies and scheduled blocks
    /// 2. User tasks
    /// 3. Work schedules (work day profiles, blocks, breaks)
    /// 4. Work profiles
    /// 5. Invitations
    /// 6. Memberships
    /// 7. Organization itself
    /// </remarks>
    Task DeleteWithCascadeAsync(Organization organization, CancellationToken cancellationToken = default);
}
