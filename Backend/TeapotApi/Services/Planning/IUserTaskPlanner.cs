namespace Services.Planning;

public interface IUserTaskPlanner
{
    /// <summary>
    /// Diagram 3: Generates a work plan for all open tasks of the given work profile.
    /// </summary>
    Task<PlanningResult> ScheduleAsync(
        Guid workProfileId, CancellationToken cancellationToken = default);
}