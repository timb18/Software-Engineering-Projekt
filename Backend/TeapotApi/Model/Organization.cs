using System.ComponentModel.DataAnnotations.Schema;

namespace Model;

public class Organization
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int MaxUsers { get; set; }

    [NotMapped]
    public int InvitationQuota
    {
        get => MaxUsers;
        set => MaxUsers = value;
    }

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();

    public ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
}
