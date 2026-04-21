namespace DataAccess.Models;

public class WorkBlock
{
    public Guid Id { get; set; }

    public Guid WorkDayProfileId { get; set; }

    public string CompanyId { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    /// <summary>HH:mm format, e.g. "09:00"</summary>
    public string StartTime { get; set; } = "09:00";

    /// <summary>HH:mm format, e.g. "17:00"</summary>
    public string EndTime { get; set; } = "17:00";

    public virtual WorkDayProfile WorkDayProfile { get; set; } = null!;
}
