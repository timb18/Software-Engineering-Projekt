using DataAccess.Models;

namespace DataAccess.Repositories;

/// <summary>
///     Data access interface for Invitation entity operations.
///     Manages organization invitations sent to users via email.
/// </summary>
/// <remarks>
///     Invitations are the primary mechanism for inviting new users to organizations.
///     This repository handles the full invitation lifecycle from creation to acceptance/expiry.
/// </remarks>
public interface IInvitationRepository
{
    /// <summary>
    ///     Finds an invitation by its unique identifier.
    /// </summary>
    /// <param name="id">The invitation GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The invitation if found, null otherwise</returns>
    Task<Invitation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Finds an open invitation for a specific email to a specific organization.
    /// </summary>
    /// <param name="organizationId">The organization GUID</param>
    /// <param name="normalizedEmail">The email being invited</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The open invitation if found, null otherwise</returns>
    /// <remarks>Only returns invitations with status=Open (not Closed, Accepted, or Expired)</remarks>
    Task<Invitation?> FindOpenAsync(Guid organizationId, string normalizedEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all pending invitations sent to a specific email address.
    /// </summary>
    /// <param name="normalizedEmail">The email address</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>List of pending invitations to this email (Open or not yet expired)</returns>
    /// <remarks>Used to show the user all invitations they can accept</remarks>
    Task<IEnumerable<Invitation>> GetPendingForEmailAsync(string normalizedEmail,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all invitations (sent and pending) for a specific organization.
    /// </summary>
    /// <param name="organizationId">The organization GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>List of all invitations for this organization</returns>
    /// <remarks>Used by organizers to manage invitations and view invitation history</remarks>
    Task<IEnumerable<Invitation>> GetForOrganizationAsync(Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all open invitations that have passed their expiry date.
    /// </summary>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>List of open invitations that should be marked expired</returns>
    /// <remarks>Used by a background job to auto-expire old invitations</remarks>
    Task<IEnumerable<Invitation>> GetExpiredOpenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a new invitation and persists it to the database.
    /// </summary>
    /// <param name="invitation">The new invitation entity</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing invitation (e.g., changes its status).
    /// </summary>
    /// <param name="invitation">The invitation with updated values</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    Task UpdateAsync(Invitation invitation, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates multiple invitations in a batch operation.
    /// </summary>
    /// <param name="invitations">Collection of invitations with updated values</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <remarks>Used for bulk expiry operations or status updates</remarks>
    Task UpdateRangeAsync(IEnumerable<Invitation> invitations, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes an invitation.
    /// </summary>
    /// <param name="invitation">The invitation to delete</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    Task DeleteAsync(Invitation invitation, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks all open invitations that have passed their expiry date as Expired.
    /// </summary>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The number of invitations marked as expired</returns>
    /// <remarks>Should be run periodically (e.g., daily) to keep invitation statuses current</remarks>
    Task<int> MarkExpiredInvitationsAsync(CancellationToken cancellationToken = default);
}