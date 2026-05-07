namespace Services.Organizations;

public interface IInvitationService
{
    /// <summary>
    /// Erstellt eine neue Einladung und versendet eine E-Mail
    /// </summary>
    Task<InvitationDto> SendInvitationAsync(
        string email,
        Guid organizationId,
        int expiryDays,
        Guid? createdByUserId = null,
        string? createdByEmail = null,
        string? firstName = null,
        string? lastName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Akzeptiert eine Einladung und fügt den Benutzer zur Organisation hinzu
    /// </summary>
    Task<bool> AcceptInvitationAsync(Guid invitationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Akzeptiert eine Einladung über den E-Mail-Link.
    /// </summary>
    Task<bool> AcceptInvitationByEmailAsync(Guid invitationId, string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lehnt eine Einladung ab
    /// </summary>
    Task<bool> RejectInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Findet eine Einladung nach ID
    /// </summary>
    Task<InvitationDto?> GetInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Findet alle offenen Einladungen für eine E-Mail-Adresse
    /// </summary>
    Task<IEnumerable<InvitationDto>> GetPendingInvitationsForEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Findet alle Einladungen für eine Organisation
    /// </summary>
    Task<IEnumerable<InvitationDto>> GetInvitationsForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Markiert abgelaufene Einladungen als expired
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
