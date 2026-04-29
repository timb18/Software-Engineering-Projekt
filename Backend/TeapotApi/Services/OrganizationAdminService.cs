using DataAccess;
using DataAccess.Models;
using DataAccess.Repositories;

namespace Services;

public class OrganizationAdminService : IOrganizationAdminService
{
    private readonly IGenericRepository<Organization> _organizationRepo;
    private readonly IGenericRepository<User> _userRepo;
    private readonly IGenericRepository<Membership> _membershipRepo;

    // Konstruktorinjection
    public OrganizationAdminService(
        IGenericRepository<Organization> organizationRepo,
        IGenericRepository<User> userRepo,
        IGenericRepository<Membership> membershipRepo)
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
            .GetFirstOrDefaultAsync(x => x.Name == request.OrganizationName, cancellationToken);
        if (existingOrganization is not null)
        {
            throw new InvalidOperationException("Organization name already exists.");
        }

        // Search for existing Organizer with email. Create if doesn't exist
        User? existingUser = await _userRepo
            .GetFirstOrDefaultAsync(x => x.Email == request.OrganizerEmail, cancellationToken);
        if (existingUser is null)
        {
            await _userRepo.AddAsync(new User()
            {
                Username = request.OrganizerUserName,
                Email = request.OrganizerEmail,
            }, cancellationToken);
        }

        // Creates new org object with all parameters
        var organization = new Organization
        {
            Name = request.OrganizationName.Trim(),
            Description = request.OrganizationDescription.Trim(),
            MaxUsers = request.maxUsers
        };
        
        // Creates org
        await _organizationRepo.AddAsync(organization, cancellationToken);

        // Create new membership object with role of the user in the org
        var membership = new Membership
        {
            UserId = (await _userRepo.GetFirstOrDefaultAsync(x => x.Email == request.OrganizerEmail, cancellationToken))!.Id,
            OrganizationId = (await _organizationRepo.GetFirstOrDefaultAsync(x => x.Name == request.OrganizationName, cancellationToken))!.Id,
            Role = ERole.Organizer
        };
        
        // Create membership
        await _membershipRepo.AddAsync(membership, cancellationToken);

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
