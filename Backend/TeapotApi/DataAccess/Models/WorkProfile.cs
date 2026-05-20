namespace DataAccess.Models;

/// <summary>
///     Defines a user's work availability and task planning configuration within an organization.
///     Contains daily work schedules and maximum daily workload limits.
/// </summary>
/// <remarks>
///     A WorkProfile is created when a user first configures their work schedule in an organization.
///     It contains:
///     - Daily work schedules (days of the week with work blocks and breaks)
///     - Maximum daily workload to prevent overallocation
///     - Planner view time range (start/end times for the UI calendar view)
///     - All tasks assigned to this user in the organization
///     The scheduling algorithm uses WorkProfile information to plan tasks within available working time.
/// </remarks>
public class WorkProfile
{
    /// <summary>Unique identifier for this work profile</summary>
    public Guid Id { get; set; }

    /// <summary>Foreign key: the membership this work profile belongs to</summary>
    public Guid MembershipId { get; set; }

    /// <summary>Maximum amount of time available for work each day (e.g., TimeSpan.FromHours(8))</summary>
    /// <remarks>Used by the scheduling algorithm to prevent overallocation on any single day</remarks>
    public TimeSpan MaxDailyLoad { get; set; }

    /// <summary>Start time for the planner view in HH:mm format (e.g. "06:00")</summary>
    /// <remarks>Defines the earliest hour shown in the calendar/scheduling UI</remarks>
    public string PlannerViewStart { get; set; } = "06:00";

    /// <summary>End time for the planner view in HH:mm format (e.g. "22:00")</summary>
    /// <remarks>Defines the latest hour shown in the calendar/scheduling UI</remarks>
    public string PlannerViewEnd { get; set; } = "22:00";

    /// <summary>Timestamp when this work profile was created</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Timestamp when the work profile was last modified (null if never edited)</summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>Navigation property: the membership this work profile belongs to</summary>
    public virtual Membership Membership { get; set; } = null!;

    /// <summary>Navigation property: all tasks assigned to this user in this organization</summary>
    public virtual ICollection<UserTask> UserTasks { get; set; } = new List<UserTask>();

    /// <summary>Navigation property: daily work schedules (days of the week with work blocks and breaks)</summary>
    public virtual ICollection<WorkDayProfile> Days { get; set; } = new List<WorkDayProfile>();
}