using DataAccess.Models;

namespace Services.Planning;

/// <summary>
/// Diagram 2: Recursive planning function with implicit backtracking.
/// Assigns tasks to free time slots respecting dependencies, block-size constraints,
/// daily budgets, and intensity rules.
/// </summary>
public class SchedulingAlgorithm
{
    /// <summary>Minimum duration of a single work block in minutes.</summary>
    public int MinBlockMinutes { get; init; } = 30;

    /// <summary>Maximum duration of a single work block in minutes.</summary>
    public int MaxBlockMinutes { get; init; } = 90;

    /// <summary>
    /// Maximum number of backtracking steps before giving up.
    /// Prevents combinatorial explosion with many tasks and tight constraints.
    /// </summary>
    public int MaxBacktrackingSteps { get; init; } = 100_000;

    /// <summary>
    /// Maximum number of free slots to attempt for a single task placement before giving up.
    /// A value of 1 means greedy (original behaviour): only the earliest valid slot is tried.
    /// Higher values allow the algorithm to skip a slot that causes a downstream conflict and
    /// try a later one — at the cost of O(MaxSlotAttempts) more work per backtracking step.
    /// Default of 5 handles the common case (e.g. deadline-constrained task blocked by an
    /// intensive-budget clash) without triggering combinatorial blowup.
    /// </summary>
    public int MaxSlotAttempts { get; init; } = 5;

    /// <summary>
    /// Tries to schedule all tasks into the state's free slots.
    /// Returns true on success (state.PlannedBlocks is populated), false on failure.
    /// </summary>
    internal bool TrySchedule(SchedulingState state) =>
        RunRecursive(state) == RecursionResult.Success;

    private enum RecursionResult { Success, Conflict }

    private RecursionResult RunRecursive(SchedulingState state)
    {
        // Hard limit: prevent infinite loops
        if (state.BacktrackingCounter >= MaxBacktrackingSteps)
            return RecursionResult.Conflict;

        // All tasks fully scheduled?
        if (state.RemainingMinutes.Values.All(m => m == 0))
            return RecursionResult.Success;

        // Determine plannable tasks: remaining > 0 and all predecessors done
        var plannable = GetPlannableTasks(state);

        if (plannable.Count == 0)
        {
            // Some tasks remain but none are plannable → dependency deadlock or all remaining are done
            if (state.RemainingMinutes.Values.All(m => m == 0))
                return RecursionResult.Success;
            state.BacktrackingCounter++;
            return RecursionResult.Conflict;
        }

        // Reset the cognitive-fatigue flag when the next available slot is on a new calendar day.
        // Research basis: Borbély sleep homeostasis model (1982) — a night's sleep fully restores
        // cognitive capacity, so the "schedule a light task after intensive work" heuristic
        // must not cross day boundaries.
        if (state.NeedsLightTaskAfter && state.LastIntensiveDay.HasValue && state.FreeSlots.Count > 0)
        {
            var nextSlotDay = DateOnly.FromDateTime(state.FreeSlots[0].Start);
            if (nextSlotDay > state.LastIntensiveDay.Value)
                state.NeedsLightTaskAfter = false;
        }

        // Try tasks in priority order. For each task: find its earliest valid slot and commit.
        // If the recursive continuation fails, undo and try the next task (bounded task-level backtracking).
        foreach (var selectedTask in state.NeedsLightTaskAfter
            ? OrderPreferLight(plannable, state)
            : OrderByPriority(plannable, state))
        {
            var remaining = state.RemainingMinutes[selectedTask.Id];

            // Tiny leftover: mark as done and continue
            if (remaining < MinBlockMinutes)
            {
                state.RemainingMinutes[selectedTask.Id] = 0;
                var quickResult = RunRecursive(state);
                if (quickResult == RecursionResult.Success) return RecursionResult.Success;
                state.RemainingMinutes[selectedTask.Id] = remaining;
                state.BacktrackingCounter++;
                continue;
            }

            // Desired block = min(remaining, maxBlock)
            var candidate = Math.Min(remaining, MaxBlockMinutes);

            // If the rest after this block would be (0, minBlock), shrink to leave exactly minBlock
            var restAfter = remaining - candidate;
            if (restAfter > 0 && restAfter < MinBlockMinutes)
                candidate = remaining - MinBlockMinutes;

            // After adjustment, candidate might have dropped below minBlock
            if (candidate < MinBlockMinutes)
            {
                if (remaining >= MinBlockMinutes && remaining <= MaxBlockMinutes)
                    candidate = remaining;
                else if (remaining < MinBlockMinutes)
                {
                    state.RemainingMinutes[selectedTask.Id] = 0;
                    var quickResult = RunRecursive(state);
                    if (quickResult == RecursionResult.Success) return RecursionResult.Success;
                    state.RemainingMinutes[selectedTask.Id] = remaining;
                    state.BacktrackingCounter++;
                    continue;
                }
                else
                {
                    state.BacktrackingCounter++;
                    continue;
                }
            }

            var result = TrySlots(state, selectedTask, candidate);
            if (result == RecursionResult.Success) return RecursionResult.Success;
        }

        state.BacktrackingCounter++;
        return RecursionResult.Conflict;
    }

    /// <summary>
    /// Finds a valid free slot for <paramref name="candidateMinutes"/> of the given task and commits it.
    /// Tries up to <see cref="MaxSlotAttempts"/> valid slots before giving up (bounded slot-level
    /// backtracking). Trying more than one slot avoids false negatives caused by the earliest slot
    /// creating a downstream conflict that a slightly later slot would not.
    /// <para>
    /// Intensive-minutes budget is a <b>soft constraint</b>: the algorithm first tries to respect the
    /// daily intensive-work cap (research: Ericsson et al. 1993 deliberate-practice limit ~4 h/day;
    /// Newport 2016 deep-work ceiling). If no slot passes that gate, it falls back to ignoring the cap
    /// so the overall plan still succeeds.
    /// </para>
    /// </summary>
    private RecursionResult TrySlots(
        SchedulingState state, UserTask task, int candidateMinutes)
    {
        // First pass: respect the intensive-minutes daily cap (scientifically grounded soft limit).
        // Second pass: relax the cap so a plan can always be found when the day only has intensive tasks.
        return TrySlotsInternal(state, task, candidateMinutes, enforceIntensiveBudget: true)
            ?? TrySlotsInternal(state, task, candidateMinutes, enforceIntensiveBudget: false)
            ?? ConflictAndCount(state);
    }

    private RecursionResult? TrySlotsInternal(
        SchedulingState state, UserTask task, int candidateMinutes,
        bool enforceIntensiveBudget = false)
    {
        var attemptsLeft = MaxSlotAttempts;

        for (var i = 0; i < state.FreeSlots.Count; i++)
        {
            var slot = state.FreeSlots[i];
            if (slot.DurationMinutes < MinBlockMinutes)
                continue;

            var effectiveDuration = Math.Min(candidateMinutes, slot.DurationMinutes);
            if (effectiveDuration < MinBlockMinutes)
                continue;

            var day = DateOnly.FromDateTime(slot.Start);
            if (!state.DailyBudgets.TryGetValue(day, out var budget))
                continue;

            // Hard limit: total daily work budget
            if (budget.RemainingTotalMinutes < effectiveDuration)
                continue;

            // Soft limit: intensive-minutes cap per day (Ericsson 1993 deliberate-practice research
            // shows ~4 h/day is the ceiling for sustained intensive cognitive work).
            // On the first pass this is enforced; on the second pass it is relaxed so the plan
            // can still be completed when all remaining tasks are intensive.
            if (enforceIntensiveBudget
                && task.Intensity == ETaskIntensity.Intensive
                && budget.RemainingIntensiveMinutes < effectiveDuration)
                continue;

            var blockEnd = slot.Start.AddMinutes(effectiveDuration);

            // Skip slots that would push the block past the task's deadline.
            // Deadline is treated as end-of-day: work may be scheduled up to midnight of the day after the deadline.
            if (task.Deadline.HasValue && blockEnd > task.Deadline.Value.Date.AddDays(1))
                continue;

            // --- Commit this slot ---
            var savedRemaining = state.RemainingMinutes[task.Id];
            var savedBudgetTotal = budget.RemainingTotalMinutes;
            var savedBudgetIntensive = budget.RemainingIntensiveMinutes;
            var savedNeedsLight = state.NeedsLightTaskAfter;
            var savedLastIntensiveDay = state.LastIntensiveDay;

            var block = new TaskBlock
            {
                TaskId = task.Id,
                StartDate = slot.Start,
                EndDate = blockEnd,
                IsFixed = false,
                Task = task
            };
            state.PlannedBlocks.Add(block);

            state.RemainingMinutes[task.Id] -= effectiveDuration;
            budget.RemainingTotalMinutes -= effectiveDuration;
            if (task.Intensity == ETaskIntensity.Intensive)
            {
                budget.RemainingIntensiveMinutes -= effectiveDuration;
                // Record which day the intensive block landed on for day-boundary reset logic
                state.NeedsLightTaskAfter = true;
                state.LastIntensiveDay = day;
            }
            else
            {
                state.NeedsLightTaskAfter = false;
            }

            // Shrink or remove used slot
            state.FreeSlots.RemoveAt(i);
            TimeSlot? trimmedSlot = null;
            if (blockEnd < slot.End)
            {
                trimmedSlot = new TimeSlot(blockEnd, slot.End);
                state.FreeSlots.Insert(i, trimmedSlot);
            }

            var result = RunRecursive(state);
            if (result == RecursionResult.Success)
                return RecursionResult.Success;

            // Recursive call failed: undo this placement.
            // Try the next valid slot (bounded by MaxSlotAttempts) before giving up.
            // This avoids false negatives where the earliest slot causes a downstream conflict
            // that a slightly later slot would not — without full slot-level exponential blowup.
            state.BacktrackingCounter++;
            state.PlannedBlocks.Remove(block);
            state.RemainingMinutes[task.Id] = savedRemaining;
            budget.RemainingTotalMinutes = savedBudgetTotal;
            budget.RemainingIntensiveMinutes = savedBudgetIntensive;
            state.NeedsLightTaskAfter = savedNeedsLight;
            state.LastIntensiveDay = savedLastIntensiveDay;

            // Restore slot
            if (trimmedSlot is not null)
                state.FreeSlots.RemoveAt(i);
            state.FreeSlots.Insert(i, slot);

            if (--attemptsLeft == 0)
                return null; // slot budget exhausted for this pass
        }

        return null; // no slot found under current constraints
    }

    private RecursionResult ConflictAndCount(SchedulingState state)
    {
        state.BacktrackingCounter++;
        return RecursionResult.Conflict;
    }

    private List<UserTask> GetPlannableTasks(SchedulingState state)
    {
        return state.Tasks
            .Where(t =>
                state.RemainingMinutes[t.Id] > 0 &&
                AllPredecessorsDone(t.Id, state))
            .ToList();
    }

    private bool AllPredecessorsDone(Guid taskId, SchedulingState state)
    {
        if (!state.Analysis.Predecessors.TryGetValue(taskId, out var preds))
            return true;
        return preds.All(p => state.RemainingMinutes.GetValueOrDefault(p, 0) == 0);
    }

    private IEnumerable<UserTask> OrderByPriority(IEnumerable<UserTask> candidates, SchedulingState state)
    {
        var critical = state.Analysis.CriticalTaskIds;
        return candidates
            .OrderBy(t => t.Deadline ?? DateTime.MaxValue)            // most urgent deadline first
            .ThenByDescending(t => critical.Contains(t.Id))           // critical as tiebreaker
            .ThenByDescending(t => (int)t.Priority)                   // higher priority first
            .ThenBy(t => state.RemainingMinutes[t.Id]);               // shorter remaining first
    }

    private IEnumerable<UserTask> OrderPreferLight(IEnumerable<UserTask> candidates, SchedulingState state)
    {
        var list = candidates.ToList();
        var lightCandidates = list.Where(t => t.Intensity == ETaskIntensity.Light).ToList();
        var otherCandidates = list.Where(t => t.Intensity != ETaskIntensity.Light).ToList();

        // Soft preference: try light tasks first, but fall through to others if they can't be placed.
        // This prevents the algorithm from getting stuck when no light tasks fit the current slot.
        return lightCandidates.Count > 0
            ? OrderByPriority(lightCandidates, state).Concat(OrderByPriority(otherCandidates, state))
            : OrderByPriority(list, state);
    }
}
