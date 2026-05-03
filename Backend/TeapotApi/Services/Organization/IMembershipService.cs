namespace Services.Organizations;

public interface IMembershipService
{
    Task LeaveOrganizationAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);

    Task RemoveUserFromOrganizationAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);
}
