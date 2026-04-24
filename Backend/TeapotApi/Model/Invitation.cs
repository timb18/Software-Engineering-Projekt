using System.ComponentModel.DataAnnotations.Schema;

namespace Model;

public class Invitation
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CreatedBy { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    [NotMapped]
    public string InviteCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public EInvitationStatus Status { get; set; }

    public User CreatedByNavigation { get; set; } = null!;

    public Organization Organization { get; set; } = null!;
}
