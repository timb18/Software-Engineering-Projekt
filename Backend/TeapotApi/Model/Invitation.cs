namespace Model;

public class Invitation
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CreatedBy { get; set; }
    public string Email { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime EditedAt { get; set; }

    public InvitationStatus Status { get; set; }

    public DateTime ExpiryDate { get; set; }

    public Organization Organization { get; set; }
}