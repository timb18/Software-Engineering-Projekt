namespace Services;

public interface IOrganizationService
{
    Task<IEnumerable<OrganizationDetailsDto>> GetOrganizationsForUserAsync(string email);
}

public class OrganizationDetailsDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MaxUsers { get; set; }
    public List<OrganizationUserDto> Users { get; set; } = [];
    public List<InvitationDto> Invites { get; set; } = [];
}

public class OrganizationUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
