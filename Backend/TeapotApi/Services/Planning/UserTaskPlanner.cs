namespace Services.Planning;

public class UserTaskPlanner : IUserTaskPlanner
{
    private readonly SchedulingAlgorithm _algorithm;

    public UserTaskPlanner(SchedulingAlgorithm algorithm)
    {
        _algorithm = algorithm;
    }
}