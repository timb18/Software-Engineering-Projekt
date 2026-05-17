using System.Net;
using Auth0.ManagementApi;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.Extensions.Options;

namespace Services.Organizations;

public class OrganizationService(
    IOrganizationRepository organizationRepository,
    IOptions<EmailOptions> emailOptions,
    IManagementApiClient managementClient) : IOrganizationService
{
    private const string PersonalWorkspaceDescription = "Auto-created personal workspace";
    private readonly EmailOptions _emailOptions = emailOptions.Value;

    public async Task<IEnumerable<OrganizationDetailsDto>> GetOrganizationsForUserAsync(string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var organizations = await organizationRepository.GetForUserAsync(normalizedEmail, cancellationToken);
        var authUsersPager =
            await managementClient.Users.ListAsync(new ListUsersRequestParameters(),
                cancellationToken: cancellationToken);

        var authUsers = authUsersPager.CurrentPage.Items.ToList();


        while (authUsersPager.HasNextPage)
        {
            await authUsersPager.GetNextPageAsync(cancellationToken);
            authUsers.AddRange(authUsersPager.CurrentPage.Items);
        }

        return organizations.Select(o => new OrganizationDetailsDto
        {
            Id = o.Id,
            Name = o.Name,
            Description = o.Description,
            MaxUsers = o.MaxUsers,
            WorkProfileId = o.Memberships
                .FirstOrDefault(m => string.Equals(m.User.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
                ?.WorkProfile?.Id,
            Users = o.Memberships
                .OrderByDescending(m => m.Role == ERole.Organizer).ThenBy(m => m.User.Email)
                .Select(m => new OrganizationUserDto
                {
                    Id = m.User.Id,
                    Email = m.User.Email,
                    Username = authUsers.FirstOrDefault(u =>
                                       string.Equals(u.Email, m.User.Email,
                                           StringComparison.InvariantCultureIgnoreCase))?
                                   .Username ??
                               m.User.Email,
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
                    OrganizationName = o.Name,
                    Email = i.Email,
                    FirstName = i.FirstName,
                    LastName = i.LastName,
                    Status = i.Status.ToString().ToLowerInvariant(),
                    CreatedAt = i.CreatedAt,
                    ExpiryDate = i.ExpiryDate,
                    InvitationLink = BuildAcceptLink(i)
                })
                .ToList()
        });
    }

    private string BuildAcceptLink(Invitation invitation) =>
        $"{TrimTrailingSlash(_emailOptions.ApiBaseUrl)}/api/Invitation/{invitation.Id}/accept-link?email={WebUtility.UrlEncode(invitation.Email)}";

    private static string TrimTrailingSlash(string url) => url.TrimEnd('/');

    public async Task RenameOrganizationAsync(RenameOrganizationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.OrganizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(command.OrganizationId));

        if (command.InitiatorUserId == Guid.Empty)
            throw new ArgumentException("InitiatorUserId is required.", nameof(command.InitiatorUserId));

        var normalizedName = command.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new ArgumentException("Organization name is required.", nameof(command.Name));

        var organization =
            await organizationRepository.GetWithMembershipsAndInvitationsAsync(command.OrganizationId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Organization not found.");

        var initiatorMembership = organization.Memberships
                                      .FirstOrDefault(m => m.UserId == command.InitiatorUserId)
                                  ?? throw new KeyNotFoundException("Initiator is not a member of this organization.");

        if (initiatorMembership.Role != ERole.Organizer)
            throw new UnauthorizedAccessException("Only organizers can rename an organization.");

        if (!string.Equals(organization.Name, normalizedName, StringComparison.Ordinal))
        {
            var existingOrganization = await organizationRepository.FindByNameAsync(normalizedName, cancellationToken);
            if (existingOrganization is not null && existingOrganization.Id != organization.Id)
                throw new InvalidOperationException("An organization with this name already exists.");
        }

        organization.Name = normalizedName;
        organization.EditedAt = DateTime.UtcNow;
        await organizationRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteOrganizationAsync(DeleteOrganizationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.OrganizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(command.OrganizationId));

        if (command.InitiatorUserId == Guid.Empty)
            throw new ArgumentException("InitiatorUserId is required.", nameof(command.InitiatorUserId));

        var organization =
            await organizationRepository.GetWithMembershipsAndInvitationsAsync(command.OrganizationId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Organization not found.");

        if (organization.MaxUsers == 1 &&
            string.Equals(organization.Description, PersonalWorkspaceDescription, StringComparison.Ordinal))
            throw new InvalidOperationException("Personal workspaces cannot be deleted.");

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