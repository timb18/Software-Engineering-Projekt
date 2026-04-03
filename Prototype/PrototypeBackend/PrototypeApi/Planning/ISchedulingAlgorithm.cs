namespace PrototypeApi.Planning;

// Strategy contract so the planning service can swap implementations later.
public interface ISchedulingAlgorithm
{
    ScheduleResult Schedule(
        IReadOnlyCollection<PlanningTask> tasks,
        WorkProfile profile,
        DateTimeOffset from,
        SchedulingOptions options);
}