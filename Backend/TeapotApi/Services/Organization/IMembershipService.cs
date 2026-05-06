namespace Services.Organizations;

public interface IMembershipService
{
    Task LeaveOrganizationAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);

    Task RemoveUserFromOrganizationAsync(Guid initiatorUserId, Guid userId, Guid organizationId, CancellationToken cancellationToken = default);

    Task UpdateRoleAsync(Guid initiatorUserId, Guid userId, Guid organizationId, string role, CancellationToken cancellationToken = default);
}
