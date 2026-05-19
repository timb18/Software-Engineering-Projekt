namespace DataAccess.Models;

public class User
{
    public Guid Id { get; set; }

    public string? AuthProviderSubject { get; set; }

    public string? Username { get; set; }

    public string? DisplayName { get; set; }

    public string Email { get; set; } = null!;

    public string? ProfileImageUrl { get; set; }

    public string? Timezone { get; set; }

    public string? BreakColor { get; set; }

    public string? BlockerColor { get; set; }

    public string? OrgColors { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public virtual ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();

    public virtual ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}
