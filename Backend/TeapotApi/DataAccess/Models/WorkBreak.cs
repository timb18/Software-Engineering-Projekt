namespace DataAccess.Models;

/// <summary>
///     Represents a break period during a work day (e.g., lunch break, coffee break).
///     Breaks are subtracted from available work time when scheduling tasks.
/// </summary>
/// <remarks>
///     Breaks are defined within work blocks and are used by the scheduling algorithm
///     to calculate actual available working time on a given day.
///     Example:
///     - WorkBlock: 9:00-17:00
///     - WorkBreak: 12:00-13:00 (lunch)
///     - Available work time: 7 hours (not 8)
/// </remarks>
public class WorkBreak
{
    /// <summary>Unique identifier for this break period</summary>
    public Guid Id { get; set; }

    /// <summary>Foreign key: the work day profile this break belongs to</summary>
    public Guid WorkDayProfileId { get; set; }

    /// <summary>Break start time in HH:mm format (e.g., "12:00")</summary>
    /// <remarks>24-hour format; should be during a work block and before EndTime</remarks>
    public string StartTime { get; set; } = "12:00";

    /// <summary>Break end time in HH:mm format (e.g., "12:30")</summary>
    /// <remarks>24-hour format; should be after StartTime</remarks>
    public string EndTime { get; set; } = "12:30";

    /// <summary>Navigation property: the work day profile this break belongs to</summary>
    public virtual WorkDayProfile WorkDayProfile { get; set; } = null!;
}