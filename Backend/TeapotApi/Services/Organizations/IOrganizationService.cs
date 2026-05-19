namespace Services.Organizations;

/// <summary>
/// Read and maintenance operations for organizations visible to the current user.
/// </summary>
public interface IOrganizationService
{
    /// <summary>
    /// Returns all organizations together with members, invitations, and the caller's work profile reference.
    /// </summary>
    Task<IEnumerable<OrganizationDetailsDto>> GetOrganizationsForUserAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames an organization after validating organizer permissions.
    /// </summary>
    Task RenameOrganizationAsync(RenameOrganizationCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an organization after checking organizer permissions and safety constraints.
    /// </summary>
    Task DeleteOrganizationAsync(DeleteOrganizationCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Command used to rename an organization.
/// </summary>
public sealed record RenameOrganizationCommand(
    Guid OrganizationId,
    Guid InitiatorUserId,
    string Name);

/// <summary>
/// Command used to delete an organization.
/// </summary>
public sealed record DeleteOrganizationCommand(
    Guid OrganizationId,
    Guid InitiatorUserId,
    string ConfirmationText);

/// <summary>
/// Aggregated organization details returned to the client.
/// </summary>
public sealed record OrganizationDetailsDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int MaxUsers { get; init; }
    public Guid? WorkProfileId { get; init; }
    public List<OrganizationUserDto> Users { get; init; } = [];
    public List<InvitationDto> Invites { get; init; } = [];
}

/// <summary>
/// Member snapshot included in organization details.
/// </summary>
public sealed record OrganizationUserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}
