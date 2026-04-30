using System.ComponentModel.DataAnnotations;
using DataAccess;
using DataAccess.Models;
using DataAccess.Repositories;

namespace Services.Organizations;

public class OrganizationAdminService(
    IOrganizationRepository organizationRepository,
    IUserRepository userRepository,
    IMembershipRepository membershipRepository,
    IUnitOfWork unitOfWork) : IOrganizationAdminService
{
    public async Task<CreateOrganizationResult> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        await using var tx = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var existingOrganization = await organizationRepository.FindByNameAsync(request.OrganizationName, cancellationToken);
        if (existingOrganization is not null)
            throw new InvalidOperationException("Organization name already exists.");

        var normalizedEmail = request.OrganizerEmail.Trim().ToLowerInvariant();
        var organizer = await userRepository.FindByEmailAsync(normalizedEmail, cancellationToken);
        if (organizer is null)
        {
            organizer = new User
            {
                Username = request.OrganizerUserName,
                Email = normalizedEmail,
            };
            await userRepository.AddAsync(organizer, cancellationToken);
        }

        var organization = new Organization
        {
            Name = request.OrganizationName.Trim(),
            Description = request.OrganizationDescription.Trim(),
            MaxUsers = request.MaxUsers
        };
        await organizationRepository.AddAsync(organization, cancellationToken);

        var membership = new Membership
        {
            UserId = organizer.Id,
            OrganizationId = organization.Id,
            Role = ERole.Organizer
        };
        await membershipRepository.AddAsync(membership, cancellationToken);

        await tx.CommitAsync(cancellationToken);

        return new CreateOrganizationResult
        {
            OrganizationId = organization.Id,
            OrganizerUserId = organizer.Id,
        };
    }

    private static void ValidateRequest(CreateOrganizationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationName))
            throw new ArgumentException("OrganizationName is required.");

        if (request.MaxUsers < 0)
            throw new ArgumentException("MaxUsers must be >= 0.");

        if (string.IsNullOrWhiteSpace(request.OrganizerUserName))
            throw new ArgumentException("OrganizerUserName is required.");

        if (string.IsNullOrWhiteSpace(request.OrganizerEmail))
            throw new ArgumentException("OrganizerEmail is required.");

        if (!new EmailAddressAttribute().IsValid(request.OrganizerEmail.Trim()))
            throw new ArgumentException("OrganizerEmail is invalid.");
    }
}
