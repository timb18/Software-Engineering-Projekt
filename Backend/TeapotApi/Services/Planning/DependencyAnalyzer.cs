using DataAccess.Models;

namespace Services.Planning;

/// <summary>
/// Diagram 1: Dependency check and critical path analysis.
/// Validates the dependency graph, performs topological sort, and marks critical tasks.
/// </summary>
public class DependencyAnalyzer
{
    /// <summary>
    /// Analyzes task dependencies, validates feasibility, and computes the critical path.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when cyclic dependencies are detected or deadlines are not achievable.
    /// </exception>
    public DependencyAnalysisResult Analyze(
        IReadOnlyList<UserTask> tasks,
        IReadOnlyList<TaskDependency> dependencies,
        DateTime projectStart)
    {
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

            earlyStart[taskId] = preds.Count > 0
                ? preds.Max(p => earlyFinish[p])
                : projectStart;

            earlyFinish[taskId] = earlyStart[taskId] + task.TimeEstimate;

            // Check deadline feasibility (deadline = end-of-day, so midnight of the next day is the exclusive bound)
            if (task.Deadline.HasValue && earlyFinish[taskId] > task.Deadline.Value.Date.AddDays(1))
                throw new InvalidOperationException(
                    $"Abhängigkeiten und Deadlines nicht vereinbar: " +
                    $"Aufgabe '{task.Name}' kann frühestens um {earlyFinish[taskId]:g} fertig werden, " +
                    $"Deadline ist {task.Deadline.Value:g}.");
        }

        // Determine project end (max of all early finishes and deadlines).
        // Deadlines are treated as end-of-day (midnight of the following day) for consistency
        // with the scheduler and ValidatePlan checks.
        var projectEnd = earlyFinish.Values.Max();
        foreach (var task in tasks)
            if (task.Deadline.HasValue && task.Deadline.Value.Date.AddDays(1) > projectEnd)
                projectEnd = task.Deadline.Value.Date.AddDays(1);

        // Backward pass: compute latest start/finish times
        var lateFinish = new Dictionary<Guid, DateTime>(tasks.Count);
        var lateStart = new Dictionary<Guid, DateTime>(tasks.Count);

        // Initialize late finish from deadlines or project end.
        // Deadline is treated as end-of-day: midnight of the following day is the exclusive upper bound.
        foreach (var taskId in topOrder)
        {
            var task = taskMap[taskId];
            lateFinish[taskId] = task.Deadline.HasValue
                ? task.Deadline.Value.Date.AddDays(1)
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

            lateStart[taskId] = lateFinish[taskId] - taskMap[taskId].TimeEstimate;
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
