namespace PrototypeApi.Planning;

// Heuristic scheduler for the MVP: dependencies first, then weighted prioritization, then greedy slot assignment.
public sealed class HeuristicSchedulingAlgorithm : ISchedulingAlgorithm
{
    public ScheduleResult Schedule(
        IReadOnlyCollection<PlanningTask> tasks,
        WorkProfile profile,
        DateTimeOffset from,
        SchedulingOptions options)
    {
        var warnings = new List<string>();

        if (tasks.Count == 0)
        {
            return CreateEmptyResult(profile, from, warnings);
        }

        var activeTasks = tasks
            .Where(task => task.Status != PlanningTaskStatus.Done)
            .ToList();

        if (activeTasks.Count == 0)
        {
            warnings.Add("No open tasks were provided; tasks marked as Done are ignored during planning.");
            return CreateEmptyResult(profile, from, warnings);
        }

        // Phase 1: normalize dependency data and build a graph that can detect cycles.
        var completedTaskIds = tasks
            .Where(task => task.Status == PlanningTaskStatus.Done)
            .Select(task => task.Id)
            .ToHashSet();

        var taskById = activeTasks.ToDictionary(task => task.Id);
        var dependencyMap = BuildDependencyMap(activeTasks, taskById, completedTaskIds, warnings);
        var graph = BuildGraph(activeTasks, dependencyMap);
        var topologicalOrder = TopologicalSort(activeTasks, graph.Successors, graph.InDegree);
        var dependencyChainHours = CalculateDependencyChainHours(topologicalOrder, graph.Successors, taskById);

        // Phase 2: score every task so the scheduler can pick the most urgent viable work first.
        var scoreByTask = activeTasks.ToDictionary(
            task => task.Id,
            task => CalculateScore(task, from, dependencyChainHours, profile, options));

        var prioritizedTasks = BuildPrioritizedOrder(activeTasks, graph.Successors, graph.InDegree, taskById, scoreByTask);

        // Phase 3: turn the work profile into concrete slots that can actually be filled.
        var planningWindow = BuildPlanningWindow(activeTasks, profile, from, options);

        warnings.AddRange(planningWindow.Warnings);

        var availableHours = planningWindow.Slots.Sum(slot => slot.Duration.TotalHours);
        var scheduledTasks = new List<ScheduledTask>();
        var conflicts = new List<ConflictTask>();
        var scheduledById = new Dictionary<Guid, ScheduledTask>();
        var minimumSlotDuration = TimeSpan.FromMinutes(options.MinimumSlotMinutes);

        // Final pass: assign each prioritized task to the earliest valid slot or emit a conflict.
        foreach (var task in prioritizedTasks)
        {
            var effectiveDependencies = dependencyMap[task.Id];
            var unresolvedDependencies = effectiveDependencies
                .Where(dependencyId => !scheduledById.ContainsKey(dependencyId))
                .ToList();

            var score = scoreByTask[task.Id];
            var chainHours = dependencyChainHours[task.Id];

            if (unresolvedDependencies.Count > 0)
            {
                conflicts.Add(CreateDependencyConflict(task, score, chainHours, unresolvedDependencies));
                continue;
            }

            var earliestStart = effectiveDependencies.Count == 0
                ? from
                : effectiveDependencies.Max(dependencyId => scheduledById[dependencyId].PlannedEnd);

            var assignment = TryAssignTask(task, earliestStart, planningWindow.Slots, minimumSlotDuration);
            if (assignment is null)
            {
                conflicts.Add(CreateCapacityConflict(task, score, chainHours, earliestStart, planningWindow.Slots));
                continue;
            }

            var constraints = CreateBaseConstraints(task, chainHours);
            if (effectiveDependencies.Count > 0)
            {
                constraints.Add($"Resolved {effectiveDependencies.Count} prerequisite task(s) before scheduling.");
            }
            else
            {
                constraints.Add("Task had no blocking dependencies.");
            }

            constraints.Add("Placed into the earliest free slot that satisfies dependencies and deadline.");

            var scheduledTask = new ScheduledTask
            {
                TaskId = task.Id,
                Title = task.Title,
                PlannedStart = assignment.Value.Start,
                PlannedEnd = assignment.Value.End,
                Deadline = task.Deadline,
                Priority = task.Priority,
                PlannedHours = RoundHours(task.EstimatedDurationHours),
                Reason = CreateReason(score, constraints)
            };

            scheduledTasks.Add(scheduledTask);
            scheduledById[task.Id] = scheduledTask;
        }

        // Capacity metrics make it easy to explain whether the current workload is realistic.
        var requestedHours = activeTasks.Sum(task => task.EstimatedDurationHours);
        var scheduledHours = scheduledTasks.Sum(task => task.PlannedHours);
        var conflictHours = conflicts.Sum(task => task.RequestedHours);
        var isOverloaded = requestedHours > availableHours + 0.0001;

        if (isOverloaded)
        {
            warnings.Add("Requested work exceeds the generated planning capacity in the current horizon.");
        }

        return new ScheduleResult
        {
            ScheduledTasks = scheduledTasks
                .OrderBy(task => task.PlannedStart)
                .ToList(),
            Conflicts = conflicts,
            Warnings = warnings,
            AppliedWorkProfile = profile,
            Capacity = new CapacitySummary
            {
                PlanningStart = from,
                PlanningEnd = planningWindow.PlanningEnd,
                RequestedHours = RoundHours(requestedHours),
                ScheduledHours = RoundHours(scheduledHours),
                ConflictHours = RoundHours(conflictHours),
                AvailableHoursInPlanningWindow = RoundHours(availableHours),
                IsOverloaded = isOverloaded
            }
        };
    }

    // Shared empty response shape for empty inputs and fully completed task sets.
    private static ScheduleResult CreateEmptyResult(
        WorkProfile profile,
        DateTimeOffset from,
        List<string> warnings)
        => new()
        {
            Warnings = warnings,
            AppliedWorkProfile = profile,
            Capacity = new CapacitySummary
            {
                PlanningStart = from,
                PlanningEnd = from,
                RequestedHours = 0,
                ScheduledHours = 0,
                ConflictHours = 0,
                AvailableHoursInPlanningWindow = 0,
                IsOverloaded = false
            }
        };

    // Removes completed and missing dependencies so the algorithm only reasons about active blockers.
    private static Dictionary<Guid, List<Guid>> BuildDependencyMap(
        IReadOnlyCollection<PlanningTask> activeTasks,
        IReadOnlyDictionary<Guid, PlanningTask> taskById,
        IReadOnlySet<Guid> completedTaskIds,
        ICollection<string> warnings)
    {
        var dependenciesByTask = new Dictionary<Guid, List<Guid>>();
        var ignoredCompletedDependencies = 0;
        var ignoredMissingDependencies = 0;

        foreach (var task in activeTasks)
        {
            var effectiveDependencies = new List<Guid>();

            foreach (var dependencyId in task.DependsOnTaskIds.Distinct())
            {
                if (taskById.ContainsKey(dependencyId))
                {
                    effectiveDependencies.Add(dependencyId);
                    continue;
                }

                if (completedTaskIds.Contains(dependencyId))
                {
                    ignoredCompletedDependencies++;
                    continue;
                }

                ignoredMissingDependencies++;
            }

            dependenciesByTask[task.Id] = effectiveDependencies;
        }

        if (ignoredCompletedDependencies > 0)
        {
            warnings.Add($"Ignored {ignoredCompletedDependencies} dependency link(s) to tasks already marked as Done.");
        }

        if (ignoredMissingDependencies > 0)
        {
            warnings.Add($"Ignored {ignoredMissingDependencies} dependency link(s) that were not part of the provided task set.");
        }

        return dependenciesByTask;
    }

    // Converts prerequisite relations into successor lists plus in-degree counts for topological traversal.
    private static GraphData BuildGraph(
        IReadOnlyCollection<PlanningTask> tasks,
        IReadOnlyDictionary<Guid, List<Guid>> dependenciesByTask)
    {
        var successors = tasks.ToDictionary(task => task.Id, _ => new List<Guid>());
        var inDegree = tasks.ToDictionary(task => task.Id, _ => 0);

        foreach (var task in tasks)
        {
            foreach (var dependencyId in dependenciesByTask[task.Id])
            {
                successors[dependencyId].Add(task.Id);
                inDegree[task.Id]++;
            }
        }

        return new GraphData(successors, inDegree);
    }

    // Kahn's algorithm guarantees dependency order and fails fast on cycles.
    private static List<PlanningTask> TopologicalSort(
        IReadOnlyCollection<PlanningTask> tasks,
        IReadOnlyDictionary<Guid, List<Guid>> successors,
        IReadOnlyDictionary<Guid, int> inDegree)
    {
        var taskById = tasks.ToDictionary(task => task.Id);
        var remainingInDegree = inDegree.ToDictionary(entry => entry.Key, entry => entry.Value);
        var ready = new Queue<PlanningTask>(tasks.Where(task => remainingInDegree[task.Id] == 0));
        var sorted = new List<PlanningTask>(tasks.Count);

        while (ready.Count > 0)
        {
            var current = ready.Dequeue();
            sorted.Add(current);

            foreach (var successorId in successors[current.Id])
            {
                remainingInDegree[successorId]--;
                if (remainingInDegree[successorId] == 0)
                {
                    ready.Enqueue(taskById[successorId]);
                }
            }
        }

        if (sorted.Count != tasks.Count)
        {
            throw new InvalidOperationException("Cyclic dependency detected in the planning input.");
        }

        return sorted;
    }

    // Estimates how much downstream work depends on each task, which approximates a critical-path signal.
    private static Dictionary<Guid, double> CalculateDependencyChainHours(
        IReadOnlyList<PlanningTask> topologicalOrder,
        IReadOnlyDictionary<Guid, List<Guid>> successors,
        IReadOnlyDictionary<Guid, PlanningTask> taskById)
    {
        var chainHours = topologicalOrder.ToDictionary(task => task.Id, _ => 0.0);

        for (var index = topologicalOrder.Count - 1; index >= 0; index--)
        {
            var task = topologicalOrder[index];
            chainHours[task.Id] = successors[task.Id]
                .Select(successorId => taskById[successorId].EstimatedDurationHours + chainHours[successorId])
                .DefaultIfEmpty(0.0)
                .Max();
        }

        return chainHours;
    }

    // Re-applies dependency constraints while picking the highest-scoring task among currently available nodes.
    private static List<PlanningTask> BuildPrioritizedOrder(
        IReadOnlyCollection<PlanningTask> tasks,
        IReadOnlyDictionary<Guid, List<Guid>> successors,
        IReadOnlyDictionary<Guid, int> inDegree,
        IReadOnlyDictionary<Guid, PlanningTask> taskById,
        IReadOnlyDictionary<Guid, ScoreBreakdown> scoreByTask)
    {
        var remainingInDegree = inDegree.ToDictionary(entry => entry.Key, entry => entry.Value);
        var ready = tasks.Where(task => remainingInDegree[task.Id] == 0).ToList();
        var ordered = new List<PlanningTask>(tasks.Count);

        while (ready.Count > 0)
        {
            ready.Sort((left, right) => CompareTasks(left, right, scoreByTask));

            var current = ready[0];
            ready.RemoveAt(0);
            ordered.Add(current);

            foreach (var successorId in successors[current.Id])
            {
                remainingInDegree[successorId]--;
                if (remainingInDegree[successorId] == 0)
                {
                    ready.Add(taskById[successorId]);
                }
            }
        }

        if (ordered.Count != tasks.Count)
        {
            throw new InvalidOperationException("Cyclic dependency detected in the planning input.");
        }

        return ordered;
    }

    // Tie-breakers keep the output deterministic when scores are equal or nearly equal.
    private static int CompareTasks(
        PlanningTask left,
        PlanningTask right,
        IReadOnlyDictionary<Guid, ScoreBreakdown> scoreByTask)
    {
        var scoreCompare = scoreByTask[right.Id].Score.CompareTo(scoreByTask[left.Id].Score);
        if (scoreCompare != 0)
        {
            return scoreCompare;
        }

        var leftDeadline = left.Deadline ?? DateTimeOffset.MaxValue;
        var rightDeadline = right.Deadline ?? DateTimeOffset.MaxValue;
        var deadlineCompare = leftDeadline.CompareTo(rightDeadline);
        if (deadlineCompare != 0)
        {
            return deadlineCompare;
        }

        var priorityCompare = ((int)right.Priority).CompareTo((int)left.Priority);
        if (priorityCompare != 0)
        {
            return priorityCompare;
        }

        var durationCompare = right.EstimatedDurationHours.CompareTo(left.EstimatedDurationHours);
        if (durationCompare != 0)
        {
            return durationCompare;
        }

        return string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
    }

    // Weighted score used by the heuristic: urgency, business priority, and downstream dependency pressure.
    private static ScoreBreakdown CalculateScore(
        PlanningTask task,
        DateTimeOffset from,
        IReadOnlyDictionary<Guid, double> dependencyChainHours,
        WorkProfile profile,
        SchedulingOptions options)
    {
        var urgencyScore = task.Deadline.HasValue
            ? 1.0 / (Math.Max(0, (task.Deadline.Value - from).TotalDays) + 1.0)
            : 0.0;

        var priorityScore = task.Priority switch
        {
            PriorityLevel.High => 3.0,
            PriorityLevel.Medium => 2.0,
            _ => 1.0
        };

        var normalizationBase = Math.Max(profile.MaxDailyLoadHours, 1.0);
        var dependencyScore = dependencyChainHours.TryGetValue(task.Id, out var chainHours)
            ? chainHours / normalizationBase
            : 0.0;

        var totalScore =
            (options.UrgencyWeight * urgencyScore) +
            (options.PriorityWeight * priorityScore) +
            (options.DependencyWeight * dependencyScore);

        return new ScoreBreakdown(
            RoundScore(totalScore),
            RoundScore(urgencyScore),
            RoundScore(priorityScore),
            RoundScore(dependencyScore));
    }

    // Generates the concrete slot pool from the work profile and planning horizon.
    private static PlanningWindow BuildPlanningWindow(
        IReadOnlyCollection<PlanningTask> tasks,
        WorkProfile profile,
        DateTimeOffset from,
        SchedulingOptions options)
    {
        var warnings = new List<string>();
        if (profile.WorkDays.Count == 0)
        {
            warnings.Add("No work days configured; default Monday-Friday work days were applied.");
        }

        var minimumSlotDuration = TimeSpan.FromMinutes(options.MinimumSlotMinutes);
        var workDays = profile.GetEffectiveWorkDays().ToHashSet();
        var requestedHours = tasks.Sum(task => task.EstimatedDurationHours);
        var latestDeadline = tasks
            .Where(task => task.Deadline.HasValue)
            .Select(task => task.Deadline!.Value)
            .DefaultIfEmpty(from)
            .Max();

        var requiredEndDay = StartOfDay(latestDeadline);
        var lastPlanningDay = StartOfDay(from).AddDays(options.MaxPlanningHorizonDays);
        var slots = new List<TimeSlot>();
        var accumulatedHours = 0.0;

        for (var currentDay = StartOfDay(from); currentDay <= lastPlanningDay; currentDay = currentDay.AddDays(1))
        {
            var daySlots = BuildDaySlots(currentDay, workDays, profile, from, minimumSlotDuration);
            slots.AddRange(daySlots);
            accumulatedHours += daySlots.Sum(slot => slot.Duration.TotalHours);

            if (currentDay >= requiredEndDay && accumulatedHours >= requestedHours)
            {
                break;
            }
        }

        if (slots.Count == 0)
        {
            warnings.Add("The applied work profile generated no usable planning slots.");
        }

        if (requiredEndDay > lastPlanningDay || accumulatedHours + 0.0001 < requestedHours)
        {
            warnings.Add("Planning horizon reached before enough capacity was generated for all requested work.");
        }

        return new PlanningWindow(
            slots,
            slots.Count == 0 ? from : slots.Max(slot => slot.End),
            warnings);
    }

    // Creates one day's usable work segments while respecting work days, breaks, daily load, and planning start.
    private static List<TimeSlot> BuildDaySlots(
        DateTimeOffset day,
        IReadOnlySet<DayOfWeek> workDays,
        WorkProfile profile,
        DateTimeOffset planningStart,
        TimeSpan minimumSlotDuration)
    {
        if (!workDays.Contains(day.DayOfWeek))
        {
            return [];
        }

        var workStart = day + profile.WorkStartTime;
        var workEnd = day + profile.WorkEndTime;
        if (workEnd <= workStart)
        {
            return [];
        }

        var breakStart = day + profile.BreakStart;
        var breakEnd = day + profile.BreakEnd;
        var hasValidBreak = breakEnd > breakStart && breakStart > workStart && breakEnd < workEnd;

        var rawSegments = hasValidBreak
            ? new List<TimeSlot>
            {
                new(workStart, breakStart),
                new(breakEnd, workEnd)
            }
            : [new(workStart, workEnd)];

        var remainingCapacity = TimeSpan.FromHours(
            Math.Min(profile.MaxDailyLoadHours, rawSegments.Sum(segment => segment.Duration.TotalHours)));

        var daySlots = new List<TimeSlot>();

        foreach (var segment in rawSegments)
        {
            if (remainingCapacity <= TimeSpan.Zero)
            {
                break;
            }

            var slotStart = segment.Start < planningStart ? planningStart : segment.Start;
            if (slotStart >= segment.End)
            {
                continue;
            }

            var availableDuration = segment.End - slotStart;
            var slotDuration = availableDuration < remainingCapacity ? availableDuration : remainingCapacity;
            if (slotDuration < minimumSlotDuration)
            {
                continue;
            }

            var slotEnd = slotStart + slotDuration;
            daySlots.Add(new TimeSlot(slotStart, slotEnd));
            remainingCapacity -= slotDuration;
        }

        return daySlots;
    }

    // First-fit greedy assignment into the earliest feasible slot; tasks are not split across multiple slots.
    private static TaskAssignment? TryAssignTask(
        PlanningTask task,
        DateTimeOffset earliestStart,
        List<TimeSlot> slots,
        TimeSpan minimumSlotDuration)
    {
        var duration = TimeSpan.FromHours(task.EstimatedDurationHours);

        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            if (slot.End <= earliestStart)
            {
                continue;
            }

            var candidateStart = slot.Start < earliestStart ? earliestStart : slot.Start;
            var candidateEnd = candidateStart + duration;

            if (candidateEnd > slot.End)
            {
                continue;
            }

            if (task.Deadline.HasValue && candidateEnd > task.Deadline.Value)
            {
                continue;
            }

            slots.RemoveAt(index);

            var insertIndex = index;
            if (candidateStart - slot.Start >= minimumSlotDuration)
            {
                slots.Insert(insertIndex, new TimeSlot(slot.Start, candidateStart));
                insertIndex++;
            }

            if (slot.End - candidateEnd >= minimumSlotDuration)
            {
                slots.Insert(insertIndex, new TimeSlot(candidateEnd, slot.End));
            }

            return new TaskAssignment(candidateStart, candidateEnd);
        }

        return null;
    }

    // Dependency conflicts are emitted when a task can only start after another unscheduled task.
    private static ConflictTask CreateDependencyConflict(
        PlanningTask task,
        ScoreBreakdown score,
        double dependencyChainHours,
        List<Guid> blockingTaskIds)
    {
        var reason = "One or more prerequisite tasks could not be scheduled first.";
        var constraints = CreateBaseConstraints(task, dependencyChainHours);
        constraints.Add(reason);

        return new ConflictTask
        {
            TaskId = task.Id,
            Title = task.Title,
            ConflictReason = reason,
            Deadline = task.Deadline,
            Priority = task.Priority,
            RequestedHours = RoundHours(task.EstimatedDurationHours),
            BlockingTaskIds = blockingTaskIds,
            Reason = CreateReason(score, constraints)
        };
    }

    // Capacity conflicts explain whether the blocker is the deadline, the horizon, or slot fragmentation.
    private static ConflictTask CreateCapacityConflict(
        PlanningTask task,
        ScoreBreakdown score,
        double dependencyChainHours,
        DateTimeOffset earliestStart,
        IReadOnlyList<TimeSlot> remainingSlots)
    {
        var feasibleWindows = remainingSlots
            .Where(slot => slot.End > earliestStart)
            .Select(slot =>
            {
                var start = slot.Start < earliestStart ? earliestStart : slot.Start;
                var end = task.Deadline.HasValue && slot.End > task.Deadline.Value
                    ? task.Deadline.Value
                    : slot.End;
                return new TimeSlot(start, end);
            })
            .Where(slot => slot.Duration > TimeSpan.Zero)
            .ToList();

        string reason;
        if (task.Deadline.HasValue && earliestStart >= task.Deadline.Value)
        {
            reason = "Dependencies push the earliest feasible start beyond the deadline.";
        }
        else if (feasibleWindows.Count == 0)
        {
            reason = task.Deadline.HasValue
                ? "No remaining work capacity exists before the deadline."
                : "No remaining work capacity exists within the planning horizon.";
        }
        else if (feasibleWindows.Max(slot => slot.Duration.TotalHours) + 0.0001 < task.EstimatedDurationHours)
        {
            reason = task.Deadline.HasValue
                ? "Task duration does not fit into any remaining slot before the deadline."
                : "Task duration does not fit into any remaining slot within the planning horizon.";
        }
        else
        {
            reason = task.Deadline.HasValue
                ? "No free slot large enough exists before the deadline."
                : "No free slot large enough exists within the planning horizon.";
        }

        var constraints = CreateBaseConstraints(task, dependencyChainHours);
        constraints.Add(reason);

        return new ConflictTask
        {
            TaskId = task.Id,
            Title = task.Title,
            ConflictReason = reason,
            Deadline = task.Deadline,
            Priority = task.Priority,
            RequestedHours = RoundHours(task.EstimatedDurationHours),
            Reason = CreateReason(score, constraints)
        };
    }

    // Human-readable reasons are included so the API can explain scheduling decisions in the UI.
    private static List<string> CreateBaseConstraints(PlanningTask task, double dependencyChainHours)
    {
        var constraints = new List<string>();

        if (task.Deadline.HasValue)
        {
            constraints.Add($"Deadline considered: {task.Deadline.Value:O}");
        }
        else
        {
            constraints.Add("No deadline provided; task was deprioritized behind time-critical work.");
        }

        constraints.Add($"Priority considered: {task.Priority}");

        if (dependencyChainHours > 0)
        {
            constraints.Add($"Downstream dependency chain considered: {RoundHours(dependencyChainHours)}h");
        }

        return constraints;
    }

    // Packs score components and explanatory notes into a single response object.
    private static SchedulingReason CreateReason(ScoreBreakdown score, List<string> constraints)
        => new()
        {
            AppliedConstraints = constraints,
            Score = score.Score,
            UrgencyScore = score.UrgencyScore,
            PriorityScore = score.PriorityScore,
            DependencyChainScore = score.DependencyChainScore
        };

    // Utility helpers keep date and rounding behavior consistent across the algorithm.
    private static DateTimeOffset StartOfDay(DateTimeOffset value)
        => new(value.Year, value.Month, value.Day, 0, 0, 0, value.Offset);

    private static double RoundHours(double value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static double RoundScore(double value)
        => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private sealed record GraphData(
        Dictionary<Guid, List<Guid>> Successors,
        Dictionary<Guid, int> InDegree);

    private sealed record PlanningWindow(
        List<TimeSlot> Slots,
        DateTimeOffset PlanningEnd,
        List<string> Warnings);

    private readonly record struct ScoreBreakdown(
        double Score,
        double UrgencyScore,
        double PriorityScore,
        double DependencyChainScore);

    private readonly record struct TaskAssignment(DateTimeOffset Start, DateTimeOffset End);

    private readonly record struct TimeSlot(DateTimeOffset Start, DateTimeOffset End)
    {
        public TimeSpan Duration => End - Start;
    }
}