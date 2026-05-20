namespace Services.Organizations;

/// <summary>
///     Membership management operations for organization members and organizers.
/// </summary>
public interface IMembershipService
{
    /// <summary>
    ///     Removes the current user from an organization.
    /// </summary>
    Task LeaveOrganizationAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes another user from an organization after organizer authorization checks.
    /// </summary>
    Task RemoveUserFromOrganizationAsync(Guid initiatorUserId, Guid userId, Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Changes a member's role inside an organization.
    /// </summary>
    Task UpdateRoleAsync(Guid initiatorUserId, Guid userId, Guid organizationId, string role,
        CancellationToken cancellationToken = default);
}