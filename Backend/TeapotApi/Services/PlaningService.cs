namespace Services;

public class PlanningService(IUserTaskPlanner taskPlanner)
{
    private readonly IUserTaskPlanner _taskPlanner = taskPlanner;
}