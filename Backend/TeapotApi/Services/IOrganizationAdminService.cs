namespace Services;

public interface IOrganizationAdminService
{
    Task<CreateOrganizationResult> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default);
}

public record CreateOrganizationRequest
{
    public string OrganizationName { get; init; } = string.Empty;

    public string OrganizationDescription { get; init; } = string.Empty;

    public int maxUsers { get; init; }

    public string OrganizerUserName { get; init; } = string.Empty;

    public string OrganizerEmail { get; init; } = string.Empty;
}

public record CreateOrganizationResult
{
    public Guid OrganizationId { get; init; }

    public Guid OrganizerUserId { get; init; }
}
