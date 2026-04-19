using DataAccess;
using Model;

namespace Services;

public class OrganizationAdminService : IOrganizationAdminService
{
    private readonly IOrganizationRepo _organizationRepo;
    private readonly IUserRepo _userRepo;
    private readonly IMembershipRepo _membershipRepo;

    // Konstruktorinjection
    public OrganizationAdminService(
        IOrganizationRepo organizationRepo,
        IUserRepo userRepo,
        IMembershipRepo membershipRepo)
    {
        _organizationRepo = organizationRepo;
        _userRepo = userRepo;
        _membershipRepo = membershipRepo;
    }

    public async Task<CreateOrganizationResult> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request); // Validate request. Throws exception if invalid

        // Search for existing organisation with email. Error if already exists
        Organization? existingOrganization = await _organizationRepo
            .GetByNameAsync(request.OrganizationName, cancellationToken);
        if (existingOrganization is not null)
        {
            throw new InvalidOperationException("Organization name already exists.");
        }

        // Search for existing Organizer with email. Create if doesn't exist
        User? existingUser = await _userRepo
            .GetByEmailAsync(request.OrganizerEmail, cancellationToken);
        if (existingUser is null)
        {
            await _userRepo.CreateAsync(new User()
            {
                UserName = request.OrganizerUserName,
                Email = request.OrganizerEmail,
            });
        }

        // Creates new org object with all parameters
        var organization = new Organization
        {
            Name = request.OrganizationName.Trim(),
            Description = request.OrganizationDescription.Trim(),
            InvitationQuota = request.maxUsers
        };
        
        // Creates org
        await _organizationRepo.CreateAsync(organization, cancellationToken);

        // Create new membership object with role of the user in the org
        var membership = new Membership
        {
            UserId = (await _userRepo.GetByEmailAsync(request.OrganizerEmail, cancellationToken))!.Id,
            OrganisationId = (await _organizationRepo.GetByNameAsync(request.OrganizationName, cancellationToken))!.Id,
            Role = Role.ORGANIZER
        };
        
        // Create membership
        await _membershipRepo.CreateAsync(membership, cancellationToken);

        return new CreateOrganizationResult
        {
            OrganizationId = organization.Id,
            OrganizerUserId = membership.UserId,
        };
    }

    // Function for validating request to create a new org
    private static void ValidateRequest(CreateOrganizationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationName))
        {
            throw new ArgumentException("OrganizationName is required.");
        }

        if (request.maxUsers < 0)
        {
            throw new ArgumentException("InvitationQuota must be >= 0.");
        }

        if (string.IsNullOrWhiteSpace(request.OrganizerUserName))
        {
            throw new ArgumentException("OrganizerUserName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OrganizerEmail))
        {
            throw new ArgumentException("OrganizerEmail is required.");
        }

        if (!request.OrganizerEmail.Contains('@'))
        {
            throw new ArgumentException("OrganizerEmail is invalid.");
        }
    }
}
