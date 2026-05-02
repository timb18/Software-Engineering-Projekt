namespace DataAccess.Models;

public partial class WorkProfile
{
    public Guid Id { get; set; }

    public Guid MembershipId { get; set; }

    public TimeSpan MaxDailyLoad { get; set; }

    public string PlannerViewStart { get; set; } = null!;

    public string PlannerViewEnd { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public virtual Membership Membership { get; set; } = null!;

    public virtual ICollection<UserTask> UserTasks { get; set; } = new List<UserTask>();

    public virtual ICollection<WorkDayProfile> WorkDayProfiles { get; set; } = new List<WorkDayProfile>();
}
