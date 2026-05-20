namespace DataAccess.Models;

/// <summary>
///     Represents a continuous block of available work time on a specific day of the week.
///     Part of a user's work schedule configuration.
/// </summary>
/// <remarks>
///     A work block defines when a user is available to work on a particular day.
///     Examples:
///     - 9:00 to 17:00 (standard 8-hour workday)
///     - 9:00 to 12:00 and 14:00 to 17:00 (split day with lunch break)
///     The scheduling algorithm finds free time within these blocks to allocate tasks.
///     Associated with a company/department for context (CompanyId, CompanyName).
/// </remarks>
public class WorkBlock
{
    /// <summary>Unique identifier for this work block</summary>
    public Guid Id { get; set; }

    /// <summary>Foreign key: the work day profile this block belongs to</summary>
    public Guid WorkDayProfileId { get; set; }

    /// <summary>Company or department identifier for this work block (for context)</summary>
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>Company or department name for display in the UI</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Block start time in HH:mm format (e.g., "09:00")</summary>
    /// <remarks>24-hour format; should be earlier than EndTime</remarks>
    public string StartTime { get; set; } = "09:00";

    /// <summary>Block end time in HH:mm format (e.g., "17:00")</summary>
    /// <remarks>24-hour format; should be later than StartTime</remarks>
    public string EndTime { get; set; } = "17:00";

    /// <summary>Navigation property: the work day profile this block belongs to</summary>
    public virtual WorkDayProfile WorkDayProfile { get; set; } = null!;
}