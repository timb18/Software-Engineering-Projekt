using System.ComponentModel.DataAnnotations.Schema;

namespace Model;

public class Membership
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid OrganizationId { get; set; }

    [NotMapped]
    public Guid OrganisationId
    {
        get => OrganizationId;
        set => OrganizationId = value;
    }

    public ERole Role { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public User User { get; set; } = null!;

    public Organization Organization { get; set; } = null!;
}
