namespace Model;

/// <summary>
/// A single work shift block on a given day, belonging to one company/org.
/// Times are stored as "HH:mm" strings to match the frontend format exactly.
/// </summary>
public record WorkBlock
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string StartTime { get; set; } = "09:00"; // HH:mm
    public string EndTime { get; set; } = "17:00";   // HH:mm
}

/// <summary>
/// A break within a work day.
/// </summary>
public record WorkBreak
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string StartTime { get; set; } = "12:00"; // HH:mm
    public string EndTime { get; set; } = "12:30";   // HH:mm
}

/// <summary>
/// The work configuration for a single weekday.
/// Day uses the same 3-letter abbreviation as the frontend: Mon, Tue, Wed, Thu, Fri, Sat, Sun.
/// </summary>
public record WorkDayProfile
{
    public string Day { get; set; } = string.Empty;
    public List<WorkBlock> Blocks { get; set; } = [];
    public List<WorkBreak> Breaks { get; set; } = [];
}

/// <summary>
/// Full weekly work profile for a user, with one entry per weekday.
/// </summary>
public record WorkProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Identifies the user this profile belongs to (username or user id string).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Seven entries, one per weekday (Mon–Sun).</summary>
    public List<WorkDayProfile> Days { get; set; } = [];

    /// <summary>Optional planner view start time "HH:mm".</summary>
    public string PlannerViewStart { get; set; } = "06:00";

    /// <summary>Optional planner view end time "HH:mm".</summary>
    public string PlannerViewEnd { get; set; } = "22:00";
}
