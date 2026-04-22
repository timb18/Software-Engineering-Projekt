namespace DataAccess.Models;

public class WorkDayProfile
{
    public Guid Id { get; set; }

    public Guid WorkProfileId { get; set; }

    /// <summary>Three-letter abbreviation: Mon, Tue, Wed, Thu, Fri, Sat, Sun</summary>
    public string Day { get; set; } = null!;

    public virtual WorkProfile WorkProfile { get; set; } = null!;

    public virtual ICollection<WorkBlock> Blocks { get; set; } = new List<WorkBlock>();

    public virtual ICollection<WorkBreak> Breaks { get; set; } = new List<WorkBreak>();
}
