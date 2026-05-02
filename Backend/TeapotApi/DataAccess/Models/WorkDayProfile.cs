namespace DataAccess.Models;

public partial class WorkDayProfile
{
    public Guid Id { get; set; }

    public Guid WorkProfileId { get; set; }

    public string Day { get; set; } = null!;

    public virtual ICollection<WorkBlock> WorkBlocks { get; set; } = new List<WorkBlock>();

    public virtual ICollection<WorkBreak> WorkBreaks { get; set; } = new List<WorkBreak>();

    public virtual WorkProfile WorkProfile { get; set; } = null!;
}
