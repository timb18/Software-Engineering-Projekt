namespace Services.Organizations;

public interface IOrganizationService
{
    Task<IEnumerable<OrganizationDetailsDto>> GetOrganizationsForUserAsync(string email);
    Task DeleteOrganizationAsync(DeleteOrganizationCommand command, CancellationToken cancellationToken = default);
}

public sealed record DeleteOrganizationCommand(
    Guid OrganizationId,
    Guid InitiatorUserId,
    string ConfirmationText);

public sealed record OrganizationDetailsDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int MaxUsers { get; init; }
    public List<OrganizationUserDto> Users { get; init; } = [];
    public List<InvitationDto> Invites { get; init; } = [];
}

public sealed record OrganizationUserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}
