namespace PrototypeApi.Planning;

// API input for a single planning run.
public sealed class PlanningRequest
{
    public List<PlanningTask> Tasks { get; init; } = [];
    public WorkProfile? WorkProfile { get; init; }
    public DateTimeOffset? PlanningStart { get; init; }
    public SchedulingOptions? Options { get; init; }
}

// Tunable weights and limits for the heuristic scheduler.
public sealed class SchedulingOptions
{
    public double UrgencyWeight { get; init; } = 0.5;
    public double PriorityWeight { get; init; } = 0.3;
    public double DependencyWeight { get; init; } = 0.2;
    public int MinimumSlotMinutes { get; init; } = 15;
    public int MaxPlanningHorizonDays { get; init; } = 180;
}

// Minimal task representation required by the scheduling engine.
public sealed class PlanningTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = string.Empty;
    public PriorityLevel Priority { get; init; } = PriorityLevel.Medium;
    public PlanningTaskStatus Status { get; init; } = PlanningTaskStatus.Open;
    public DateTimeOffset? Deadline { get; init; }
    public double EstimatedDurationHours { get; init; }
    public List<Guid> DependsOnTaskIds { get; init; } = [];
}

public enum PriorityLevel
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum PlanningTaskStatus
{
    Open,
    InProgress,
    Done
}

// Describes when and how much the user can realistically work.
public sealed class WorkProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? UserId { get; init; }
    public TimeSpan WorkStartTime { get; init; } = TimeSpan.FromHours(9);
    public TimeSpan WorkEndTime { get; init; } = TimeSpan.FromHours(17);
    public TimeSpan BreakStart { get; init; } = TimeSpan.FromHours(12.5);
    public TimeSpan BreakEnd { get; init; } = TimeSpan.FromHours(13);
    public double MaxDailyLoadHours { get; init; } = 6.0;
    public List<DayOfWeek> WorkDays { get; init; } =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday
    ];

    public static WorkProfile CreateDefault() => new();

    public IReadOnlyCollection<DayOfWeek> GetEffectiveWorkDays()
        => WorkDays.Count > 0
            ? WorkDays.Distinct().ToArray()
            :
            [
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            ];
}

// Top-level result returned by the planning endpoint.
public sealed class ScheduleResult
{
    public List<ScheduledTask> ScheduledTasks { get; init; } = [];
    public List<ConflictTask> Conflicts { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public CapacitySummary Capacity { get; init; } = new();
    public WorkProfile AppliedWorkProfile { get; init; } = WorkProfile.CreateDefault();
}

// Capacity snapshot to explain whether the current task set overloads the user.
public sealed class CapacitySummary
{
    public DateTimeOffset PlanningStart { get; init; }
    public DateTimeOffset PlanningEnd { get; init; }
    public double RequestedHours { get; init; }
    public double ScheduledHours { get; init; }
    public double ConflictHours { get; init; }
    public double AvailableHoursInPlanningWindow { get; init; }
    public bool IsOverloaded { get; init; }
}

// A task that received a concrete time slot.
public sealed class ScheduledTask
{
    public Guid TaskId { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTimeOffset PlannedStart { get; init; }
    public DateTimeOffset PlannedEnd { get; init; }
    public DateTimeOffset? Deadline { get; init; }
    public PriorityLevel Priority { get; init; }
    public double PlannedHours { get; init; }
    public SchedulingReason Reason { get; init; } = new();
}

// A task that could not be scheduled under the current constraints.
public sealed class ConflictTask
{
    public Guid TaskId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ConflictReason { get; init; } = string.Empty;
    public DateTimeOffset? Deadline { get; init; }
    public PriorityLevel Priority { get; init; }
    public double RequestedHours { get; init; }
    public List<Guid> BlockingTaskIds { get; init; } = [];
    public SchedulingReason Reason { get; init; } = new();
}

// Stores coarse explanation data so the scheduling decision is transparent.
public sealed class SchedulingReason
{
    public List<string> AppliedConstraints { get; init; } = [];
    public double Score { get; init; }
    public double UrgencyScore { get; init; }
    public double PriorityScore { get; init; }
    public double DependencyChainScore { get; init; }
}