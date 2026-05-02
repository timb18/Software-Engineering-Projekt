namespace DataAccess.Models;

public partial class Membership
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid OrganizationId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public virtual Organization Organization { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    public virtual WorkProfile? WorkProfile { get; set; }
    
    public ERole Role { get; set; }
}
