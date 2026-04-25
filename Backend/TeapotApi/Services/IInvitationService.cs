namespace Services;

public interface IInvitationService
{
    /// <summary>
    /// Erstellt eine neue Einladung und versendet eine E-Mail
    /// </summary>
    Task<InvitationDto> SendInvitationAsync(
        string email,
        Guid organizationId,
        Guid? createdByUserId = null,
        string? createdByEmail = null,
        string? firstName = null,
        string? lastName = null);

    /// <summary>
    /// Akzeptiert eine Einladung und fügt den Benutzer zur Organisation hinzu
    /// </summary>
    Task<bool> AcceptInvitationAsync(Guid invitationId, Guid userId);

    /// <summary>
    /// Akzeptiert eine Einladung über den E-Mail-Link.
    /// </summary>
    Task<bool> AcceptInvitationByEmailAsync(Guid invitationId, string email);

    /// <summary>
    /// Lehnt eine Einladung ab
    /// </summary>
    Task<bool> RejectInvitationAsync(Guid invitationId);

    /// <summary>
    /// Findet eine Einladung nach ID
    /// </summary>
    Task<InvitationDto?> GetInvitationAsync(Guid invitationId);

    /// <summary>
    /// Findet alle offenen Einladungen für eine E-Mail-Adresse
    /// </summary>
    Task<IEnumerable<InvitationDto>> GetPendingInvitationsForEmailAsync(string email);

    /// <summary>
    /// Findet alle Einladungen für eine Organisation
    /// </summary>
    Task<IEnumerable<InvitationDto>> GetInvitationsForOrganizationAsync(Guid organizationId);

    /// <summary>
    /// Löscht abgelaufene Einladungen
    /// </summary>
    Task<int> CleanupExpiredInvitationsAsync();
}

public class InvitationDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string InvitationLink { get; set; } = string.Empty;
}
