using DataAccess.Models;

namespace Services.Planning;

public record TimeSlot(DateTime Start, DateTime End, Guid? OrganizationId = null)
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
    IReadOnlyList<TaskBlock> PlannedBlocks,
    IReadOnlyList<string> Warnings);
