namespace DataAccess.Models;

public class WorkProfile
{
    public Guid Id { get; set; }

    public Guid MembershipId { get; set; }

    public TimeSpan MaxDailyLoad { get; set; }

    /// <summary>Planner view start time in HH:mm format, e.g. "06:00"</summary>
    public string PlannerViewStart { get; set; } = "06:00";

    /// <summary>Planner view end time in HH:mm format, e.g. "22:00"</summary>
    public string PlannerViewEnd { get; set; } = "22:00";

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public virtual Membership Membership { get; set; } = null!;

    public virtual ICollection<UserTask> UserTasks { get; set; } = new List<UserTask>();

    public virtual ICollection<WorkDayProfile> Days { get; set; } = new List<WorkDayProfile>();
}