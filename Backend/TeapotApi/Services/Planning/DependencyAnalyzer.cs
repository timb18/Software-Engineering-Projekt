using DataAccess.Models;

namespace Services.Planning;

/// <summary>
/// Diagram 1: Dependency check and critical path analysis.
/// Validates the dependency graph, performs topological sort, and marks critical tasks.
/// </summary>
public class DependencyAnalyzer
{
    /// <summary>
    /// Analyzes a list of tasks and their dependencies to calculate the project schedule,
    /// critical path, and topological ordering.
    /// </summary>
    /// <param name="tasks">The list of user tasks to be analyzed.</param>
    /// <param name="dependencies">The list of task dependencies defining task relationships.</param>
    /// <param name="projectStart">The starting date and time for the project planning.</param>
    /// <param name="fixedTaskTimes">Optional dictionary mapping task IDs to fixed start and end times.</param>
    /// <return>A DependencyAnalysisResult containing the topological order, critical tasks, and predecessor lists.</return>
    /// <exception cref="InvalidOperationException">Thrown when a cyclic dependency is detected that prevents valid scheduling.</exception>
    public DependencyAnalysisResult Analyze(
        IReadOnlyList<UserTask> tasks,
        IReadOnlyList<TaskDependency> dependencies,
        DateTime projectStart,
        IReadOnlyDictionary<Guid, (DateTime Start, DateTime End)>? fixedTaskTimes = null,
        IReadOnlyDictionary<Guid, TimeSpan>? effectiveDurations = null)
    {
        TimeSpan DurationOf(UserTask t) =>
            effectiveDurations != null && effectiveDurations.TryGetValue(t.Id, out var d)
                ? d
                : t.TimeEstimate;

        if (tasks.Count == 0)
            return new DependencyAnalysisResult([], new HashSet<Guid>(), new Dictionary<Guid, IReadOnlyList<Guid>>());

        var taskIds = tasks.Select(t => t.Id).ToHashSet();

        // Build adjacency lists (only for tasks in our planning set)
        var successors = tasks.ToDictionary(t => t.Id, _ => new List<Guid>());
        var predecessorsDict = tasks.ToDictionary(t => t.Id, _ => new List<Guid>());

        foreach (var dep in dependencies)
        {
            if (!taskIds.Contains(dep.DependsOnTaskId) || !taskIds.Contains(dep.TaskId))
                continue;
            successors[dep.DependsOnTaskId].Add(dep.TaskId);
            predecessorsDict[dep.TaskId].Add(dep.DependsOnTaskId);
        }

        // Kahn's algorithm: topological sort + cycle detection
        var inDegree = tasks.ToDictionary(t => t.Id, t => predecessorsDict[t.Id].Count);
        var queue = new Queue<Guid>(tasks.Where(t => inDegree[t.Id] == 0).Select(t => t.Id));
        var topOrder = new List<Guid>(tasks.Count);

        while (queue.Count > 0)
        {
            var taskId = queue.Dequeue();
            topOrder.Add(taskId);
            foreach (var succ in successors[taskId])
            {
                if (--inDegree[succ] == 0)
                    queue.Enqueue(succ);
            }
        }

        if (topOrder.Count != tasks.Count)
            throw new InvalidOperationException(
                "Zyklische Abhängigkeiten. Planung nicht möglich.");

        var taskMap = tasks.ToDictionary(t => t.Id);

        // Forward pass: compute earliest start/finish times
        var earlyStart = new Dictionary<Guid, DateTime>(tasks.Count);
        var earlyFinish = new Dictionary<Guid, DateTime>(tasks.Count);

        foreach (var taskId in topOrder)
        {
            var task = taskMap[taskId];
            var preds = predecessorsDict[taskId];

            if (fixedTaskTimes != null && fixedTaskTimes.TryGetValue(taskId, out var fixedTime))
            {
                // Fixed task: use the actual block window, not a duration estimate.
                // Predecessors must still finish before the fixed block starts.
                var predFinish = preds.Count > 0 ? preds.Max(p => earlyFinish[p]) : projectStart;
                if (predFinish > fixedTime.Start)
                    throw new InvalidOperationException(
                        $"Abhängigkeiten nicht vereinbar: Vorgänger von '{task.Name}' können nicht " +
                        $"vor dem fixierten Zeitblock ({fixedTime.Start:g}) abgeschlossen werden.");
                earlyStart[taskId] = fixedTime.Start;
                earlyFinish[taskId] = fixedTime.End;
                // No deadline check: the user explicitly pinned this time.
            }
            else
            {
                earlyStart[taskId] = preds.Count > 0
                    ? preds.Max(p => earlyFinish[p])
                    : projectStart;

                earlyFinish[taskId] = earlyStart[taskId] + DurationOf(task);

                // Check deadline feasibility: deadline is an exact timestamp (inclusive).
                if (task.Deadline.HasValue && earlyFinish[taskId] > task.Deadline.Value)
                    throw new InvalidOperationException(
                        $"Abhängigkeiten und Deadlines nicht vereinbar: " +
                        $"Aufgabe '{task.Name}' kann frühestens um {earlyFinish[taskId]:g} fertig werden, " +
                        $"Deadline ist {task.Deadline.Value:g}.");
            }
        }

        // Determine project end (max of all early finishes and deadlines).
        // Deadlines are treated as end-of-day (midnight of the following day) for consistency
        // with the scheduler and ValidatePlan checks.
        var projectEnd = earlyFinish.Values.Max();
        foreach (var task in tasks)
            if (task.Deadline.HasValue && task.Deadline.Value > projectEnd)
                projectEnd = task.Deadline.Value;

        // Backward pass: compute latest start/finish times
        var lateFinish = new Dictionary<Guid, DateTime>(tasks.Count);
        var lateStart = new Dictionary<Guid, DateTime>(tasks.Count);

        // Initialize late finish from deadlines or project end.
        // Deadline is treated as end-of-day: midnight of the following day is the exclusive upper bound.
        // Fixed tasks: use their actual block end time.
        foreach (var taskId in topOrder)
        {
            var task = taskMap[taskId];
            if (fixedTaskTimes != null && fixedTaskTimes.TryGetValue(taskId, out var ft))
                lateFinish[taskId] = ft.End;
            else
                lateFinish[taskId] = task.Deadline.HasValue
                    ? task.Deadline.Value
                    : projectEnd;
        }

        // Process in reverse topological order
        foreach (var taskId in Enumerable.Reverse(topOrder))
        {
            var succs = successors[taskId];
            if (succs.Count > 0)
            {
                var minSuccLateStart = succs.Min(s => lateStart.GetValueOrDefault(s, lateFinish[taskId]));
                if (minSuccLateStart < lateFinish[taskId])
                    lateFinish[taskId] = minSuccLateStart;
            }

            // Fixed tasks: lateStart is the actual block start (duration is pinned).
            if (fixedTaskTimes != null && fixedTaskTimes.TryGetValue(taskId, out var fixedT))
                lateStart[taskId] = fixedT.Start;
            else
                lateStart[taskId] = lateFinish[taskId] - DurationOf(taskMap[taskId]);
        }

        // Identify critical tasks: slack (lateStart - earlyStart) <= 0
        var criticalIds = new HashSet<Guid>();
        foreach (var taskId in topOrder)
        {
            var slack = lateStart[taskId] - earlyStart[taskId];
            if (slack <= TimeSpan.Zero)
                criticalIds.Add(taskId);
        }

        var predsReadOnly = predecessorsDict.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<Guid>)kvp.Value.AsReadOnly());

        return new DependencyAnalysisResult(topOrder.AsReadOnly(), criticalIds, predsReadOnly);
    }
}
