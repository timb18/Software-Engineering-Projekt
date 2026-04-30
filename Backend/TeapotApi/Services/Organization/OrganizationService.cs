using DataAccess.Models;
using DataAccess.Repositories;

namespace Services.Organizations;

public class OrganizationService(
    IOrganizationRepository organizationRepository) : IOrganizationService
{
    public async Task<IEnumerable<OrganizationDetailsDto>> GetOrganizationsForUserAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var organizations = await organizationRepository.GetForUserAsync(normalizedEmail, cancellationToken);

        return organizations.Select(o => new OrganizationDetailsDto
        {
            Id = o.Id,
            Name = o.Name,
            Description = o.Description,
            MaxUsers = o.MaxUsers,
            Users = o.Memberships
                .OrderBy(m => m.User.Username)
                .Select(m => new OrganizationUserDto
                {
                    Id = m.User.Id,
                    Email = m.User.Email,
                    Username = m.User.Username ?? m.User.Email,
                    Role = m.Role.ToString().ToLowerInvariant()
                })
                .ToList(),
            Invites = o.Invitations
                .Where(i => i.Status == EInvitationStatus.Open && i.ExpiryDate > DateTime.UtcNow)
                .OrderBy(i => i.CreatedAt)
                .Select(i => new InvitationDto
                {
                    Id = i.Id,
                    OrganizationId = i.OrganizationId,
                    Email = i.Email,
                    FirstName = i.FirstName,
                    LastName = i.LastName,
                    Status = i.Status.ToString().ToLowerInvariant(),
                    CreatedAt = i.CreatedAt,
                    ExpiryDate = i.ExpiryDate,
                    InvitationLink = string.Empty
                })
                .ToList()
        });
    }

    public async Task DeleteOrganizationAsync(DeleteOrganizationCommand command, CancellationToken cancellationToken = default)
    {
        if (command.OrganizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(command.OrganizationId));

        if (command.InitiatorUserId == Guid.Empty)
            throw new ArgumentException("InitiatorUserId is required.", nameof(command.InitiatorUserId));

        var organization = await organizationRepository.GetWithMembershipsAndInvitationsAsync(command.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException("Organization not found.");

        var initiatorMembership = organization.Memberships
            .FirstOrDefault(m => m.UserId == command.InitiatorUserId)
            ?? throw new KeyNotFoundException("Initiator is not a member of this organization.");

        if (initiatorMembership.Role != ERole.Organizer)
            throw new UnauthorizedAccessException("Only organizers can delete an organization.");

        var otherOrganizers = organization.Memberships
            .Where(m => m.UserId != command.InitiatorUserId && m.Role == ERole.Organizer)
            .ToList();

        if (otherOrganizers.Count > 0)
            throw new InvalidOperationException("The organization cannot be deleted while there are other organizers.");

        if (!string.Equals(organization.Name, command.ConfirmationText?.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Confirmation text does not match the organization name.");

        await organizationRepository.DeleteWithCascadeAsync(organization, cancellationToken);
    }
}
