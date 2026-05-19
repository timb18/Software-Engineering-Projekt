namespace Services.Organizations;

public interface IInvitationService
{
    /// <summary>
    /// Creates a new invitation and sends an email.
    /// </summary>
    Task<InvitationDto> SendInvitationAsync(
        string email,
        Guid organizationId,
        int expiryDays,
        Guid? createdByUserId = null,
        string? createdByEmail = null,
        string? firstName = null,
        string? lastName = null,
        string? publicApiBaseUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts an invitation and adds the user to the organization.
    /// </summary>
    Task<bool> AcceptInvitationAsync(Guid invitationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts an invitation through the email link.
    /// </summary>
    Task<bool> AcceptInvitationByEmailAsync(Guid invitationId, string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects an invitation.
    /// </summary>
    Task<bool> RejectInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an invitation by ID.
    /// </summary>
    Task<InvitationDto?> GetInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all open invitations for an email address.
    /// </summary>
    Task<IEnumerable<InvitationDto>> GetPendingInvitationsForEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all invitations for an organization.
    /// </summary>
    Task<IEnumerable<InvitationDto>> GetInvitationsForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks expired invitations as expired.
    /// </summary>
    Task<int> CleanupExpiredInvitationsAsync(CancellationToken cancellationToken = default);
}

public sealed record InvitationDto
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public string? OrganizationName { get; init; }
    public string Email { get; init; } = null!;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string Status { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public string InvitationLink { get; init; } = string.Empty;
    public bool? EmailSent { get; init; }
    public string? EmailError { get; init; }
}
