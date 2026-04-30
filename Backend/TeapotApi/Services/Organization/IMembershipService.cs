namespace Services;

public interface IMembershipService
{
    Task LeaveOrganizationAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);
}
