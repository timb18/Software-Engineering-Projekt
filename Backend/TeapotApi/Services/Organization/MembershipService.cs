using DataAccess.Models;
using DataAccess.Repositories;

namespace Services.Organizations;

public class MembershipService(IMembershipRepository membershipRepository) : IMembershipService
{
    public async Task LeaveOrganizationAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default)
        => await RemoveMembershipAsync(userId, organizationId, cancellationToken);

    public async Task RemoveUserFromOrganizationAsync(Guid initiatorUserId, Guid userId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var initiatorMembership = await membershipRepository.FindOrganizerAsync(organizationId, initiatorUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Only organizers can remove members from the organization.");

        await RemoveMembershipAsync(userId, organizationId, cancellationToken);
    }

    public async Task UpdateRoleAsync(Guid initiatorUserId, Guid userId, Guid organizationId, string role, CancellationToken cancellationToken = default)
    {
        if (initiatorUserId == Guid.Empty)
            throw new ArgumentException("InitiatorUserId is required.", nameof(initiatorUserId));

        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));

        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));

        var initiatorMembership = await membershipRepository.FindOrganizerAsync(organizationId, initiatorUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Only organizers can change member roles.");

        if (initiatorMembership.UserId == userId)
            throw new InvalidOperationException("You cannot change your own role.");

        var membership = await membershipRepository.FindAsync(userId, organizationId, cancellationToken)
            ?? throw new KeyNotFoundException("Membership not found.");

        var nextRole = ParseRole(role);
        if (membership.Role == nextRole)
            return;

        if (membership.Role == ERole.Organizer && nextRole == ERole.User)
        {
            var organizerCount = await membershipRepository.CountOrganizersAsync(organizationId, cancellationToken);
            if (organizerCount <= 1)
                throw new InvalidOperationException("Cannot demote the only organizer of this organization.");
        }

        membership.Role = nextRole;
        membership.EditedAt = DateTime.UtcNow;
        await membershipRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveMembershipAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));

        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));

        var membership = await membershipRepository.FindWithWorkProfileAsync(userId, organizationId, cancellationToken)
            ?? throw new KeyNotFoundException("Membership not found.");

        if (membership.Role == ERole.Organizer)
        {
            var organizerCount = await membershipRepository.CountOrganizersAsync(organizationId, cancellationToken);
            if (organizerCount <= 1)
                throw new InvalidOperationException("Cannot leave: you are the only organizer of this organization. Transfer ownership first or delete the organization.");
        }

        await membershipRepository.DeleteWithWorkProfileDataAsync(membership, cancellationToken);
    }

    private static ERole ParseRole(string role) =>
        (role?.Trim().ToLowerInvariant() ?? string.Empty) switch
        {
            "admin" or "organizer" => ERole.Organizer,
            "user" or "member" => ERole.User,
            _ => throw new ArgumentException("Role must be 'admin' or 'user'.", nameof(role))
        };
}
