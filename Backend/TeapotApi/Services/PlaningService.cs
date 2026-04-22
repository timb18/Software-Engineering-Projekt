namespace Services;

public class PlanningService
{
    private readonly IUserTaskPlanner _taskPlanner;

    public PlanningService(IUserTaskPlanner taskPlanner)
    {
        _taskPlanner = taskPlanner;
    }
}