using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Models;

public partial class UserTask
{
    public Guid Id { get; set; }

    public Guid WorkProfileId { get; set; }

    public string? Description { get; set; }

    public bool IsFixed { get; set; }
    [Column("priority")]
    public ETaskPriority Priority { get; set; }
    
    [Column("intensity")]
    public ETaskIntensity Intensity { get; set; }

    public TimeSpan TimeEstimate { get; set; }

    public DateTime? Deadline { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public string Name { get; set; } = null!;

    public DateTime EarlyStart { get; set; }

    public DateTime EarlyFinish { get; set; }

    public DateTime LateStart { get; set; }

    public DateTime LateFinish { get; set; }
    [Column("status")]
    public string Status { get; set; } = "todo"!;

    public bool Allowsplitting { get; set; }

    public int Minblockduration { get; set; }

    public int Maxblockduration { get; set; }

    public int Maxsplits { get; set; }

    public virtual WorkProfile WorkProfile { get; set; } = null!;
}

