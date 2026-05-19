namespace Services.Planning;

/// <summary>
/// Reserved entry point for planning-related orchestration logic.
/// </summary>
public class PlanningService(IUserTaskPlanner taskPlanner)
{
    private readonly IUserTaskPlanner _taskPlanner = taskPlanner;
}