namespace DataAccess.Models;

public partial class WorkBlock
{
    public Guid Id { get; set; }

    public Guid WorkDayProfileId { get; set; }

    public string CompanyId { get; set; } = null!;

    public string CompanyName { get; set; } = null!;

    public string StartTime { get; set; } = null!;

    public string EndTime { get; set; } = null!;

    public virtual WorkDayProfile WorkDayProfile { get; set; } = null!;
}
