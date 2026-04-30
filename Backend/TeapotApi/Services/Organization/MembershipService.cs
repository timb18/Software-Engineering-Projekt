using DataAccess.Repositories;

namespace Services.Organizations;

public class MembershipService(IMembershipRepository membershipRepository) : IMembershipService
{
    public async Task LeaveOrganizationAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));

        var membership = await membershipRepository.FindWithWorkProfileAsync(userId, organizationId, cancellationToken)
            ?? throw new KeyNotFoundException("Membership not found.");

        await membershipRepository.DeleteWithWorkProfileDataAsync(membership, cancellationToken);
    }
}
