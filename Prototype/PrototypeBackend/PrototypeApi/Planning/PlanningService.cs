namespace PrototypeApi.Planning;

// Thin application service that normalizes request data and delegates scheduling.
public sealed class PlanningService
{
    private readonly ISchedulingAlgorithm _algorithm;

    public PlanningService(ISchedulingAlgorithm algorithm)
    {
        _algorithm = algorithm;
    }

    public ScheduleResult GeneratePlan(PlanningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Optional request parts are normalized here so the algorithm always receives a full configuration.
        var tasks = request.Tasks ?? [];
        var profile = request.WorkProfile ?? WorkProfile.CreateDefault();
        var options = request.Options ?? new SchedulingOptions();

        // Fast validation keeps bad requests out of the scheduling core and produces clearer API errors.
        ValidateRequest(tasks, profile, options);

        var planningStart = request.PlanningStart ?? DateTimeOffset.UtcNow;
        var result = _algorithm.Schedule(tasks, profile, planningStart, options);

        if (request.WorkProfile is null)
        {
            result.Warnings.Add("Default work profile applied (09:00-17:00, Monday-Friday, 6h daily load).");
        }

        return result;
    }

    // These checks cover malformed input that would otherwise produce confusing scheduling results.
    private static void ValidateRequest(
        IReadOnlyCollection<PlanningTask> tasks,
        WorkProfile profile,
        SchedulingOptions options)
    {
        var duplicateIds = tasks
            .GroupBy(task => task.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            throw new ArgumentException("Planning tasks must have unique IDs.");
        }

        var invalidDurationTask = tasks.FirstOrDefault(task => task.EstimatedDurationHours <= 0);
        if (invalidDurationTask is not null)
        {
            throw new ArgumentException($"Task '{invalidDurationTask.Title}' must define a positive estimated duration.");
        }

        if (profile.WorkEndTime <= profile.WorkStartTime)
        {
            throw new ArgumentException("WorkEndTime must be later than WorkStartTime.");
        }

        if (profile.MaxDailyLoadHours <= 0)
        {
            throw new ArgumentException("MaxDailyLoadHours must be greater than 0.");
        }

        if (options.MinimumSlotMinutes <= 0)
        {
            throw new ArgumentException("MinimumSlotMinutes must be greater than 0.");
        }

        if (options.MaxPlanningHorizonDays <= 0)
        {
            throw new ArgumentException("MaxPlanningHorizonDays must be greater than 0.");
        }

        if (options.UrgencyWeight < 0 || options.PriorityWeight < 0 || options.DependencyWeight < 0)
        {
            throw new ArgumentException("Scheduling weights cannot be negative.");
        }

        if (options.UrgencyWeight == 0 && options.PriorityWeight == 0 && options.DependencyWeight == 0)
        {
            throw new ArgumentException("At least one scheduling weight must be greater than 0.");
        }
    }
}