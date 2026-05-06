using DataAccess.Models;
using DataAccess.Repositories;

namespace Services.Planning;

/// <summary>
/// Diagram 3: Work plan generation – orchestrates dependency analysis and recursive scheduling.
/// </summary>
public class UserTaskPlanner(
    IUserTaskRepository taskRepository,
    IWorkProfileRepository workProfileRepository,
    ITaskDependencyRepository dependencyRepository,
    ITaskBlockRepository taskBlockRepository,
    DependencyAnalyzer dependencyAnalyzer,
    SchedulingAlgorithm schedulingAlgorithm) : IUserTaskPlanner
{
    /// <summary>
    /// Percentage of the daily total budget reserved for intensive tasks.
    /// </summary>
    private const int IntensiveBudgetPercent = 50;

    public async Task<PlanningResult> ScheduleAsync(
        Guid workProfileId, CancellationToken cancellationToken = default)
    {
        // Load open tasks (todo + in-progress)
        var allTasks = (await taskRepository.GetByWorkProfileAsync(workProfileId, cancellationToken)).ToList();

        // Auto-complete: if all scheduled blocks of a task are in the past, mark it as done
        var now = DateTime.UtcNow;
        var existingBlocks = (await taskBlockRepository.GetByWorkProfileAsync(workProfileId, cancellationToken)).ToList();
        var blocksByTask = existingBlocks.GroupBy(b => b.TaskId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var task in allTasks.Where(t => t.Status != "done"))
        {
            if (blocksByTask.TryGetValue(task.Id, out var taskBlocks)
                && taskBlocks.Count > 0
                && taskBlocks.All(b => b.EndDate <= now))
            {
                task.Status = "done";
                await taskRepository.UpdateAsync(task, cancellationToken);
            }
        }

        var openTasks = allTasks.Where(t => t.Status != "done").ToList();

        if (openTasks.Count == 0)
            return new PlanningResult(true, null, 0, []);

        // Load work profile with day profiles, blocks and breaks
        var workProfile = await workProfileRepository.GetByIdAsync(workProfileId, cancellationToken);
        if (workProfile is null)
            return new PlanningResult(false, "Kein Arbeitsprofil gefunden.", 0, []);

        // Load blockings (appointments / busy intervals)
        var blockings = await workProfileRepository.GetTimeIntervalsAsync(workProfileId, cancellationToken);

        // Load task dependencies
        var dependencies = await dependencyRepository.GetByWorkProfileAsync(workProfileId, cancellationToken);

        // Load existing fixed blocks — these are preserved by ReplaceAsync and must not be overwritten
        var fixedBlocks = (await taskBlockRepository.GetByWorkProfileAsync(workProfileId, cancellationToken))
            .Where(b => b.IsFixed)
            .ToList();

        // Fixed tasks already have their time locked in fixed blocks; exclude from dynamic scheduling
        var tasksToSchedule = openTasks.Where(t => !t.IsFixed).ToList();

        // Determine planning period: today → latest deadline + 1 day (fallback: 30 days)
        var projectStart = DateTime.UtcNow.Date;
        var projectEnd = openTasks
            .Where(t => t.Deadline.HasValue)
            .Select(t => t.Deadline!.Value.Date)
            .DefaultIfEmpty(projectStart.AddDays(30))
            .Max()
            .AddDays(1);

        // Build fixed-task time map: for each fixed task, determine its actual time window
        // from the earliest fixed block start to the latest fixed block end.
        var fixedTaskTimes = openTasks
            .Where(t => t.IsFixed)
            .Select(t => new
            {
                t.Id,
                Blocks = fixedBlocks.Where(b => b.TaskId == t.Id).ToList()
            })
            .Where(x => x.Blocks.Count > 0)
            .ToDictionary(
                x => x.Id,
                x => (Start: x.Blocks.Min(b => b.StartDate), End: x.Blocks.Max(b => b.EndDate)));

        // --- Diagram 1: Dependency analysis ---
        DependencyAnalysisResult analysis;
        try
        {
            analysis = dependencyAnalyzer.Analyze(openTasks, dependencies, projectStart, fixedTaskTimes);
        }
        catch (InvalidOperationException ex)
        {
            return new PlanningResult(false, ex.Message, 0, []);
        }

        // Generate free time slots from work profile
        var freeSlots = GenerateTimeSlots(workProfile, projectStart, projectEnd);

        // Ensure slots are sorted chronologically. GenerateTimeSlots produces them in order,
        // but explicit sorting guards against any edge-case (e.g., overlapping work blocks on the
        // same day). The scheduling algorithm relies on early-slot-first (EFS) ordering.
        freeSlots.Sort((a, b) => a.Start.CompareTo(b.Start));

        // Remove slots that are already in the past so tasks are never scheduled retroactively
        freeSlots.RemoveAll(s => s.End <= now);
        for (var i = 0; i < freeSlots.Count; i++)
        {
            if (freeSlots[i].Start < now)
                freeSlots[i] = new TimeSlot(now, freeSlots[i].End);
        }

        // Remove blocking intervals from free slots
        foreach (var blocking in blockings)
            SubtractInterval(freeSlots, blocking.StartDate, blocking.EndDate);

        // Remove fixed task blocks from free slots so dynamic tasks cannot overlap them
        foreach (var fb in fixedBlocks)
            SubtractInterval(freeSlots, fb.StartDate, fb.EndDate);

        // Initialize remaining durations for dynamically-scheduled tasks only.
        // Subtract minutes already covered by past blocks so partial progress is respected.
        // Fixed-task predecessors are absent from this dict; AllPredecessorsDone treats them as done via GetValueOrDefault
        var remainingMinutes = tasksToSchedule.ToDictionary(
            t => t.Id,
            t =>
            {
                var total = (int)t.TimeEstimate.TotalMinutes;
                if (!blocksByTask.TryGetValue(t.Id, out var pastBlocks)) return total;
                var alreadyDone = (int)pastBlocks
                    .Where(b => b.EndDate <= now)
                    .Sum(b => (b.EndDate - b.StartDate).TotalMinutes);
                return Math.Max(0, total - alreadyDone);
            });

        // Initialize daily budgets from MaxDailyLoad
        var dailyBudgets = BuildDailyBudgets(workProfile, freeSlots);

        var state = new SchedulingState
        {
            Tasks = tasksToSchedule,
            RemainingMinutes = remainingMinutes,
            FreeSlots = freeSlots,
            DailyBudgets = dailyBudgets,
            Analysis = analysis,
            NeedsLightTaskAfter = false,
            BacktrackingCounter = 0
        };

        // --- Diagram 2: Recursive scheduling ---
        var success = schedulingAlgorithm.TrySchedule(state);

        if (!success)
            return new PlanningResult(
                false,
                "Kein gültiger Plan innerhalb der aktuellen Rahmenbedingungen gefunden.",
                state.BacktrackingCounter,
                []);

        // Validate the produced plan
        if (!ValidatePlan(state))
            return new PlanningResult(
                false,
                "Der erzeugte Plan ist inkonsistent.",
                state.BacktrackingCounter,
                state.PlannedBlocks);

        // Persist the plan
        await taskBlockRepository.ReplaceAsync(workProfileId, state.PlannedBlocks, cancellationToken);

        // Sync EarlyStart / EarlyFinish on each task to its scheduled block window so the
        // frontend calendar can read the actual scheduled times from the task endpoint.
        var plannedBlocksByTask = state.PlannedBlocks
            .GroupBy(b => b.TaskId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var task in tasksToSchedule)
        {
            if (!plannedBlocksByTask.TryGetValue(task.Id, out var taskBlocks) || taskBlocks.Count == 0)
                continue;
            task.EarlyStart = taskBlocks.Min(b => b.StartDate);
            task.EarlyFinish = taskBlocks.Max(b => b.EndDate);
            await taskRepository.UpdateAsync(task, cancellationToken);
        }

        return new PlanningResult(true, null, state.BacktrackingCounter, state.PlannedBlocks);
    }

    // -------------------------------------------------------------------------
    // Time-slot generation
    // -------------------------------------------------------------------------

    private static List<TimeSlot> GenerateTimeSlots(
        WorkProfile workProfile, DateTime from, DateTime to)
    {
        var slots = new List<TimeSlot>();
        var dayMap = workProfile.Days.ToDictionary(d => d.Day, StringComparer.OrdinalIgnoreCase);

        // Only use work blocks that belong to this profile's own organization.
        // Other companies' blocks represent committed time for those organizations, not free slots.
        var ownOrgId = workProfile.Membership?.OrganizationId.ToString();

        for (var date = from.Date; date < to.Date; date = date.AddDays(1))
        {
            var dayAbbrev = ToDayAbbreviation(date.DayOfWeek);
            if (!dayMap.TryGetValue(dayAbbrev, out var dayProfile))
                continue;

            var ownBlocks = ownOrgId is not null
                ? dayProfile.Blocks.Where(b => string.Equals(b.CompanyId, ownOrgId, StringComparison.OrdinalIgnoreCase)).ToList()
                : dayProfile.Blocks.ToList();

            foreach (var block in ownBlocks)
            {
                var blockStart = ParseTime(date, block.StartTime);
                var blockEnd = ParseTime(date, block.EndTime);
                if (blockEnd <= blockStart) continue;

                // Remove breaks that fall within this work block
                var breaks = dayProfile.Breaks
                    .Select(b => (Start: ParseTime(date, b.StartTime), End: ParseTime(date, b.EndTime)))
                    .Where(b => b.Start >= blockStart && b.End <= blockEnd && b.End > b.Start)
                    .OrderBy(b => b.Start)
                    .ToList();

                var cursor = blockStart;
                foreach (var brk in breaks)
                {
                    if (cursor < brk.Start)
                        slots.Add(new TimeSlot(cursor, brk.Start));
                    cursor = brk.End;
                }
                if (cursor < blockEnd)
                    slots.Add(new TimeSlot(cursor, blockEnd));
            }
        }

        return slots;
    }

    private static DateTime ParseTime(DateTime date, string hhMm)
    {
        var parts = hhMm.Split(':');
        return date.Date
            .AddHours(int.Parse(parts[0]))
            .AddMinutes(int.Parse(parts[1]));
    }

    private static string ToDayAbbreviation(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Mon",
        DayOfWeek.Tuesday => "Tue",
        DayOfWeek.Wednesday => "Wed",
        DayOfWeek.Thursday => "Thu",
        DayOfWeek.Friday => "Fri",
        DayOfWeek.Saturday => "Sat",
        DayOfWeek.Sunday => "Sun",
        _ => throw new ArgumentOutOfRangeException(nameof(day))
    };

    /// <summary>Removes a busy interval from the free-slot list, splitting where necessary.</summary>
    private static void SubtractInterval(
        List<TimeSlot> slots, DateTime busyStart, DateTime busyEnd)
    {
        for (var i = slots.Count - 1; i >= 0; i--)
        {
            var s = slots[i];
            if (busyEnd <= s.Start || busyStart >= s.End) continue;

            slots.RemoveAt(i);
            if (s.Start < busyStart)
                slots.Insert(i, new TimeSlot(s.Start, busyStart));
            if (busyEnd < s.End)
                slots.Insert(i + (s.Start < busyStart ? 1 : 0), new TimeSlot(busyEnd, s.End));
        }
    }

    private static Dictionary<DateOnly, DailyBudget> BuildDailyBudgets(
        WorkProfile workProfile, List<TimeSlot> slots)
    {
        var configuredMax = (int)workProfile.MaxDailyLoad.TotalMinutes;
        if (configuredMax <= 0) configuredMax = 480; // fallback: 8 h

        // Sum available slot minutes per day
        var slotMinutesPerDay = slots
            .GroupBy(s => DateOnly.FromDateTime(s.Start))
            .ToDictionary(g => g.Key, g => g.Sum(s => s.DurationMinutes));

        var result = new Dictionary<DateOnly, DailyBudget>();
        foreach (var (day, slotMinutes) in slotMinutesPerDay)
        {
            var effectiveMax = Math.Min(configuredMax, slotMinutes);
            var maxIntensive = effectiveMax * IntensiveBudgetPercent / 100;
            result[day] = new DailyBudget
            {
                RemainingTotalMinutes = effectiveMax,
                RemainingIntensiveMinutes = maxIntensive
            };
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // Plan validation
    // -------------------------------------------------------------------------

    private bool ValidatePlan(SchedulingState state)
    {
        // No overlapping blocks across different tasks on the same day.
        // Blocks of the SAME task cannot overlap each other by construction (each block consumes
        // a unique free slot), so we only check cross-task overlaps.
        var sorted = state.PlannedBlocks.OrderBy(b => b.StartDate).ToList();
        for (var i = 1; i < sorted.Count; i++)
            if (sorted[i].TaskId != sorted[i - 1].TaskId &&
                sorted[i].StartDate < sorted[i - 1].EndDate)
                return false;

        // Dependencies respected: first block of successor starts after last block of predecessor
        var taskLastFinish = state.PlannedBlocks
            .GroupBy(b => b.TaskId)
            .ToDictionary(g => g.Key, g => g.Max(b => b.EndDate));

        var taskFirstStart = state.PlannedBlocks
            .GroupBy(b => b.TaskId)
            .ToDictionary(g => g.Key, g => g.Min(b => b.StartDate));

        foreach (var (taskId, preds) in state.Analysis.Predecessors)
        {
            if (!taskFirstStart.TryGetValue(taskId, out var succStart)) continue;
            foreach (var predId in preds)
            {
                if (!taskLastFinish.TryGetValue(predId, out var predFinish)) continue;
                if (succStart < predFinish)
                    return false;
            }
        }

        // Block durations within min/max bounds
        foreach (var block in state.PlannedBlocks)
        {
            var duration = (int)(block.EndDate - block.StartDate).TotalMinutes;
            if (duration < schedulingAlgorithm.MinBlockMinutes ||
                duration > schedulingAlgorithm.MaxBlockMinutes)
                return false;
        }

        // All task blocks finish before or on the task's deadline (end-of-day)
        var taskMap = state.Tasks.ToDictionary(t => t.Id);
        foreach (var (taskId, lastFinish) in taskLastFinish)
        {
            if (!taskMap.TryGetValue(taskId, out var task)) continue;
            if (task.Deadline.HasValue && lastFinish > task.Deadline.Value.Date.AddDays(1))
                return false;
        }

        return true;
    }
}