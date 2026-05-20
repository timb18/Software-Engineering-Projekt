using System.ComponentModel.DataAnnotations.Schema;

namespace DataAccess.Models;

/// <summary>
///     Represents a task assigned to a user within an organization's work profile.
///     Tasks have priorities, deadlines, estimated durations, and can depend on other tasks.
/// </summary>
/// <remarks>
///     Tasks support:
///     - Different priority levels (Low, Medium, High) and intensity levels (Light, Normal, Intensive)
///     - Optional deadlines for completion
///     - Task dependencies (one task must complete before another starts)
///     - Status tracking (todo, in-progress, done)
///     - Fixed or flexible scheduling (fixed tasks must be done on specific dates)
///     The scheduling algorithm uses task information to automatically create time blocks
///     within the user's available work time. EarlyStart/EarlyFinish and LateStart/LateFinish
///     are computed during the scheduling process using critical path analysis.
/// </remarks>
public class UserTask
{
    /// <summary>Unique identifier for this task</summary>
    public Guid Id { get; set; }

    /// <summary>Foreign key: the work profile this task belongs to</summary>
    public Guid WorkProfileId { get; set; }

    /// <summary>
    ///     Optional organization the task is assigned to. The scheduler will only use shifts
    ///     tagged with this org. Null means: use the workprofile's owning (personal) org.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Task name/title displayed in the UI</summary>
    public string Name { get; set; } = null!;

    /// <summary>Detailed description of what the task involves</summary>
    public string? Description { get; set; }

    /// <summary>Whether this task has a fixed schedule (must be done on specific dates)</summary>
    /// <remarks>Fixed tasks (e.g., meetings) must be scheduled exactly when specified; flexible tasks can move</remarks>
    public bool IsFixed { get; set; }

    /// <summary>Priority level of the task (Low, Medium, High)</summary>
    /// <remarks>Affects scheduling priority; higher priority tasks are typically scheduled earlier</remarks>
    public ETaskPriority Priority { get; set; }

    /// <summary>Intensity level required to complete the task (Light, Normal, Intensive)</summary>
    /// <remarks>Affects daily workload distribution; intensive tasks limit how many can be scheduled per day</remarks>
    public ETaskIntensity Intensity { get; set; }

    /// <summary>Estimated duration to complete the task</summary>
    /// <remarks>Used by the scheduling algorithm to allocate time blocks</remarks>
    public TimeSpan TimeEstimate { get; set; }

    /// <summary>Optional deadline for task completion</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>Timestamp when the task was created</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Timestamp when the task was last edited (null if never edited)</summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>Current status of the task (todo, in-progress, or done)</summary>
    /// <remarks>String values: "todo" (default), "in-progress", "done"</remarks>
    [Column("status")]
    public string Status { get; set; } = "todo";

    /// <summary>Earliest possible start time for this task (computed during scheduling)</summary>
    /// <remarks>Part of critical path analysis; considering all dependencies and constraints</remarks>
    public DateTime EarlyStart { get; set; }

    /// <summary>Earliest possible finish time for this task (computed during scheduling)</summary>
    /// <remarks>EarlyStart + TimeEstimate</remarks>
    public DateTime EarlyFinish { get; set; }

    /// <summary>Latest possible start time without delaying the project (computed during scheduling)</summary>
    /// <remarks>Part of critical path analysis; considering all dependencies and constraints</remarks>
    public DateTime LateStart { get; set; }

    /// <summary>Latest possible finish time without delaying the project (computed during scheduling)</summary>
    /// <remarks>Based on deadline or dependent task constraints</remarks>
    public DateTime LateFinish { get; set; }

    /// <summary>Navigation property: the work profile this task belongs to</summary>
    public virtual WorkProfile? WorkProfile { get; set; }

    /// <summary>Not mapped to database: list of task IDs that this task depends on (loaded from TaskDependency table)</summary>
    [NotMapped]
    public List<Guid> DependsOnTaskIds { get; set; } = [];
}