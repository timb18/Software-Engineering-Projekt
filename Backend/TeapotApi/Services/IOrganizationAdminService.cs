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

    public int InvitationQuota { get; init; }

    public string AdminUserName { get; init; } = string.Empty;

    public string AdminEmail { get; init; } = string.Empty;
}

public record CreateOrganizationResult
{
    public Guid OrganizationId { get; init; }

    public Guid AdminUserId { get; init; }
}
