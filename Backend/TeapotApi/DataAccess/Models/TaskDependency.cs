namespace DataAccess.Models;

/// <summary>
///     Represents a dependency relationship between two tasks.
///     Indicates that one task must be completed before another can start.
/// </summary>
/// <remarks>
///     TaskDependency defines precedence constraints for the scheduling algorithm.
///     For example, if Task A depends on Task B:
///     - Task B must be scheduled and completed before Task A can start
///     - This affects the critical path calculation
///     - The scheduler respects these constraints when creating TaskBlocks
///     Dependencies form a directed acyclic graph (DAG) representing the project workflow.
/// </remarks>
public class TaskDependency
{
    /// <summary>Foreign key: the task that has a dependency (must finish before the other task)</summary>
    public Guid TaskId { get; set; }

    /// <summary>Foreign key: the task that this task depends on (must start after the other finishes)</summary>
    public Guid DependsOnTaskId { get; set; }

    /// <summary>Navigation property: the task that must be completed first (the dependency/prerequisite)</summary>
    public virtual UserTask DependsOnTask { get; set; } = null!;

    /// <summary>Navigation property: the task that depends on the other task (the dependent/successor)</summary>
    public virtual UserTask Task { get; set; } = null!;
}