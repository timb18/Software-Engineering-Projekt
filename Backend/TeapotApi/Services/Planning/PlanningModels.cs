using DataAccess.Models;

namespace Services.Planning;

public record TimeSlot(DateTime Start, DateTime End)
{
    public int DurationMinutes => (int)(End - Start).TotalMinutes;
}

public record DependencyAnalysisResult(
    IReadOnlyList<Guid> TopologicalOrder,
    IReadOnlySet<Guid> CriticalTaskIds,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> Predecessors);

public record PlanningResult(
    bool Success,
    string? ErrorMessage,
    int BacktrackingCount,
    IReadOnlyList<TaskBlock> PlannedBlocks);

internal class DailyBudget
{
    public int RemainingTotalMinutes { get; set; }
    public int RemainingIntensiveMinutes { get; set; }
}

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
    /// Used to reset <see cref="NeedsLightTaskAfter"/> when crossing a day boundary
    /// (sleep resets cognitive fatigue — Borbély sleep homeostasis model, 1982).
    /// </summary>
    public DateOnly? LastIntensiveDay { get; set; }
    public int BacktrackingCounter { get; set; }
    public List<TaskBlock> PlannedBlocks { get; } = [];
}
