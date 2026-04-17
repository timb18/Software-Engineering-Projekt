namespace Services;

public class UserTaskPlanner : IUserTaskPlanner
{
    private readonly SchedulingAlgorithm _algorithm;
    public UserTaskPlanner(SchedulingAlgorithm algorithm)
    {
        _algorithm = algorithm;
    }
}
