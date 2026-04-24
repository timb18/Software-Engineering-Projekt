using System.ComponentModel.DataAnnotations.Schema;

namespace Model;

public class User
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    [NotMapped]
    public string UserName
    {
        get => Username;
        set => Username = value;
    }

    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();

    public ICollection<Invitation> CreatedInvitations { get; set; } = new List<Invitation>();
}
