using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Models;

public class UserTask
{
    public Guid Id { get; set; }

    public Guid WorkProfileId { get; set; }

    public string? Description { get; set; }

    public bool IsFixed { get; set; }

    public ETaskPriority Priority { get; set; }

    public ETaskIntensity Intensity { get; set; }

    public TimeSpan TimeEstimate { get; set; }

    public DateTime? Deadline { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>todo | in-progress | done</summary>
    [Column("status")]
    public string Status { get; set; } = "todo";

    public DateTime EarlyStart { get; set; }

    public DateTime EarlyFinish { get; set; }

    public DateTime LateStart { get; set; }

    public DateTime LateFinish { get; set; }

    public virtual WorkProfile? WorkProfile { get; set; }

    public bool AllowSplitting { get; set; } = true;

    public TimeSpan MinBlockDuration { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan MaxBlockDuration { get; set; } = TimeSpan.FromHours(4);

    public int MaxSplits { get; set; } = 5;
}