namespace DataAccess.Models;

public partial class User
{
    public Guid Id { get; set; }

    public string? Username { get; set; }

    public string Email { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public string? AuthProviderSubject { get; set; }

    public string? DisplayName { get; set; }

    public string? ProfileImageUrl { get; set; }

    public string? Timezone { get; set; }

    public virtual ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();

    public virtual ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}
