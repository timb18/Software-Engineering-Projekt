namespace Services.Organizations;

/// <summary>
///     Administrative organization operations, such as creating a new workspace.
/// </summary>
public interface IOrganizationAdminService
{
    /// <summary>
    ///     Creates a new organization and ensures the organizer exists.
    /// </summary>
    Task<CreateOrganizationResult> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Command object used to create a new organization.
/// </summary>
public record CreateOrganizationRequest
{
    public string OrganizationName { get; init; } = string.Empty;

    public string OrganizationDescription { get; init; } = string.Empty;

    public int MaxUsers { get; init; }

    public string OrganizerEmail { get; init; } = string.Empty;
}

/// <summary>
///     Result returned after a successful organization creation.
/// </summary>
public record CreateOrganizationResult
{
    public Guid OrganizationId { get; init; }

    public Guid OrganizerUserId { get; init; }
}