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

        // Search for existing adminuser with email. Error if already exists
        User? existingUser = await _userRepo
            .GetByEmailAsync(request.AdminEmail, cancellationToken);
        if (existingUser is not null)
        {
            throw new InvalidOperationException("Admin email already exists.");
        }

        // Creates new org object with all parameters
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.OrganizationName.Trim(),
            Description = request.OrganizationDescription.Trim(),
            InvitationQuota = request.InvitationQuota
        };

        // Creates new adminuser object with all parameters + timestamp
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            UserName = request.AdminUserName.Trim(),
            Email = request.AdminEmail.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        // Create new membership object with role of the user in the org
        var membership = new Membership
        {
            UserId = adminUser.Id,
            OrganisationId = organization.Id,
            Role = Role.ORGANIZER
        };

        // Creates org
        await _organizationRepo.CreateAsync(organization, cancellationToken);

        // Creates user
        await _userRepo.CreateAsync(adminUser, cancellationToken);

        // Create membership
        await _membershipRepo.CreateAsync(membership, cancellationToken);

        return new CreateOrganizationResult
        {
            OrganizationId = organization.Id,
            AdminUserId = adminUser.Id
        };
    }

    // Function for validating request to create a new org
    private static void ValidateRequest(CreateOrganizationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationName))
        {
            throw new ArgumentException("OrganizationName is required.");
        }

        if (request.InvitationQuota < 0)
        {
            throw new ArgumentException("InvitationQuota must be >= 0.");
        }

        if (string.IsNullOrWhiteSpace(request.AdminUserName))
        {
            throw new ArgumentException("AdminUserName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.AdminEmail))
        {
            throw new ArgumentException("AdminEmail is required.");
        }

        if (!request.AdminEmail.Contains('@'))
        {
            throw new ArgumentException("AdminEmail is invalid.");
        }
    }
}
