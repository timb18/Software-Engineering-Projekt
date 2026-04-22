namespace DataAccess.Models;

public class WorkProfileTimeInterval
{
    public Guid WorkProfileId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public virtual WorkProfile WorkProfile { get; set; } = null!;
}