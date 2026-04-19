namespace DataAccess.Models;

public class WorkProfile
{
    public Guid Id { get; set; }

    public Guid MembershipId { get; set; }

    public TimeSpan MaxDailyLoad { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public virtual Membership Membership { get; set; } = null!;

    public virtual ICollection<UserTask> UserTasks { get; set; } = new List<UserTask>();
}