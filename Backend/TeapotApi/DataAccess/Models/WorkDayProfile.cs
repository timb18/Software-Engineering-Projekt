namespace DataAccess.Models;

/// <summary>
///     Represents the work schedule for a specific day of the week (e.g., Monday, Tuesday, etc.)
///     Contains work availability blocks and break times for that day.
/// </summary>
/// <remarks>
///     Part of a WorkProfile, each WorkDayProfile defines:
///     - The day of the week (Mon, Tue, Wed, etc.)
///     - Work availability blocks (when the user is available to work)
///     - Break times during the work day
///     Multiple work blocks can exist for a single day (e.g., 9-12, 2-5 with a lunch break 12-2).
///     The scheduling algorithm uses these to find free time for task allocation.
/// </remarks>
public class WorkDayProfile
{
    /// <summary>Unique identifier for this work day profile</summary>
    public Guid Id { get; set; }

    /// <summary>Foreign key: the work profile this day belongs to</summary>
    public Guid WorkProfileId { get; set; }

    /// <summary>Three-letter day abbreviation (Mon, Tue, Wed, Thu, Fri, Sat, Sun)</summary>
    /// <remarks>ISO standard format, case-sensitive (e.g., "Mon" not "monday")</remarks>
    public string Day { get; set; } = null!;

    /// <summary>Navigation property: the parent work profile</summary>
    public virtual WorkProfile WorkProfile { get; set; } = null!;

    /// <summary>Navigation property: all work time blocks for this day (e.g., 9:00-17:00)</summary>
    public virtual ICollection<WorkBlock> Blocks { get; set; } = new List<WorkBlock>();

    /// <summary>Navigation property: all breaks during the work day (e.g., lunch 12:00-13:00)</summary>
    public virtual ICollection<WorkBreak> Breaks { get; set; } = new List<WorkBreak>();
}