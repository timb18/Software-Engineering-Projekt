using DataAccess.Models;

namespace Services.Planning;

public record TimeSlot(DateTime Start, DateTime End, DateOnly? WorkDay = null)
{
    /// <summary>
    /// Duration of the slot in whole minutes.
    /// </summary>
    public int DurationMinutes => (int)(End - Start).TotalMinutes;
    public DateOnly Day => WorkDay ?? DateOnly.FromDateTime(Start);
}

/// <summary>
/// Output of the dependency analysis step used by the planner.
/// </summary>
public record DependencyAnalysisResult(
    IReadOnlyList<Guid> TopologicalOrder,
    IReadOnlySet<Guid> CriticalTaskIds,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> Predecessors);

/// <summary>
/// Result returned by the planning pipeline.
/// </summary>
public record PlanningResult(
    bool Success,
    string? ErrorMessage,
    int BacktrackingCount,
    IReadOnlyList<TaskBlock> PlannedBlocks,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Mutable budget information used by the recursive scheduler.
/// </summary>
internal class DailyBudget
{
    public int RemainingTotalMinutes { get; set; }
    public int RemainingIntensiveMinutes { get; set; }
}

/// <summary>
/// In-memory state shared across the scheduling algorithm recursion.
/// </summary>
internal class SchedulingState
{
    public required IReadOnlyList<UserTask> Tasks { get; init; }
    public required Dictionary<Guid, int> RemainingMinutes { get; init; }
    public required List<TimeSlot> FreeSlots { get; init; }
    public required Dictionary<DateOnly, DailyBudget> DailyBudgets { get; init; }
    public required DependencyAnalysisResult Analysis { get; init; }
    public bool NeedsLightTaskAfter { get; set; }
    /// <summary>
    /// The calendar day on which the most recent intensive block was placed.
    /// Used to reset <see cref="NeedsLightTaskAfter"/> when crossing a day boundary.
    /// </summary>
    public DateOnly? LastIntensiveDay { get; set; }
    public int BacktrackingCounter { get; set; }
    public List<TaskBlock> PlannedBlocks { get; } = [];
}
