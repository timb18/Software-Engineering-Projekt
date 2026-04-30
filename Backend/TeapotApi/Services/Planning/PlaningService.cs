namespace Services.Planning;

public class PlanningService(IUserTaskPlanner taskPlanner)
{
    private readonly IUserTaskPlanner _taskPlanner = taskPlanner;
}