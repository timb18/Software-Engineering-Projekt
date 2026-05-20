using DataAccess.Models;

namespace DataAccess.Repositories;

/// <summary>
///     Data access interface for User entity operations.
///     Provides methods for finding users, managing user accounts, and validating email uniqueness.
/// </summary>
/// <remarks>
///     UserRepository abstracts all database operations related to user accounts,
///     allowing the service layer to work with users without knowing database details.
/// </remarks>
public interface IUserRepository
{
    /// <summary>
    ///     Finds a user by their Auth0 authentication provider subject identifier.
    /// </summary>
    /// <param name="subject">The Auth0 unique subject identifier (from auth_provider_subject column)</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The user if found, null otherwise</returns>
    /// <remarks>Used during OAuth login to find or create the user account</remarks>
    Task<User?> FindByAuthProviderSubjectAsync(string subject, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Finds a user by their email address.
    /// </summary>
    /// <param name="normalizedEmail">The email address to search for</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The user if found, null otherwise</returns>
    /// <remarks>Email addresses are unique; returns exactly one user or none</remarks>
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Finds a user by their unique identifier.
    /// </summary>
    /// <param name="id">The user GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The user if found, null otherwise</returns>
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if an email is taken by another user (different ID).
    /// </summary>
    /// <param name="userId">The user ID to exclude from the search</param>
    /// <param name="normalizedEmail">The email to check</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>True if another user has this email, false otherwise</returns>
    /// <remarks>Used during profile updates to ensure email uniqueness</remarks>
    Task<bool> IsEmailTakenByOtherAsync(Guid userId, string normalizedEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a new user account and persists it to the database.
    /// </summary>
    /// <param name="user">The new user entity to create</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing user account.
    /// </summary>
    /// <param name="user">The user entity with updated values</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <remarks>Persists all property changes to the database</remarks>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}