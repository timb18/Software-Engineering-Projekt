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
}