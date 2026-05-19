namespace DataAccess.Models;

/// <summary>
/// Represents a time period during which a work profile is active.
/// Allows work profiles to have multiple active periods or seasonal definitions.
/// </summary>
/// <remarks>
/// WorkProfileTimeIntervals define when a work profile's schedule is in effect.
/// This allows for:
/// - Seasonal work schedules (different hours in summer vs. winter)
/// - Project-based time periods (team only available during project duration)
/// - Flexible work arrangements with multiple active periods
/// 
/// The scheduling algorithm considers active intervals when creating TaskBlocks.
/// </remarks>
public class WorkProfileTimeInterval
{
    /// <summary>Foreign key: the work profile this time interval belongs to</summary>
    public Guid WorkProfileId { get; set; }

    /// <summary>Start date of this interval (inclusive)</summary>
    public DateTime StartDate { get; set; }

    /// <summary>End date of this interval (inclusive)</summary>
    public DateTime EndDate { get; set; }

    /// <summary>Navigation property: the work profile this time interval belongs to</summary>
    public virtual WorkProfile WorkProfile { get; set; } = null!;
}