namespace DataAccess.Models;

public partial class Invitation
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string Email { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Organization Organization { get; set; } = null!;
    
    public EInvitationStatus Status { get; set; }
}
