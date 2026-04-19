namespace Services;

public class PlaningService
{
    private readonly IUserTaskPlanner _taskPlanner;

    public PlaningService(IUserTaskPlanner taskPlanner)
    {
        _taskPlanner = taskPlanner;
    }
}