namespace DataAccess.Models;

public class WorkBreak
{
    public Guid Id { get; set; }

    public Guid WorkDayProfileId { get; set; }

    /// <summary>HH:mm format, e.g. "12:00"</summary>
    public string StartTime { get; set; } = "12:00";

    /// <summary>HH:mm format, e.g. "12:30"</summary>
    public string EndTime { get; set; } = "12:30";

    public virtual WorkDayProfile WorkDayProfile { get; set; } = null!;
}
