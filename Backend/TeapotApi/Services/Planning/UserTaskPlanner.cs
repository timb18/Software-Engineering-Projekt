using DataAccess;
using DataAccess.Models;
using DataAccess.Repositories;

namespace Services.Planning;

/// <summary>
///     Diagram 3: Work plan generation – orchestrates dependency analysis and recursive scheduling.
/// </summary>
public class UserTaskPlanner(
    IUserTaskRepository taskRepository,
    IWorkProfileRepository workProfileRepository,
    ITaskDependencyRepository dependencyRepository,
    ITaskBlockRepository taskBlockRepository,
    IRecurringBlockerRepository recurringBlockerRepository,
    DependencyAnalyzer dependencyAnalyzer,
    GreedyScheduler greedyScheduler,
    IUnitOfWork unitOfWork) : IUserTaskPlanner
{
    public async Task<PlanningResult> ScheduleAsync(
        Guid workProfileId, CancellationToken cancellationToken = default)
    {
        // Load open tasks (todo + in-progress)
        var allTasks = (await taskRepository.GetByWorkProfileAsync(workProfileId, cancellationToken)).ToList();

        // Auto-complete: if all scheduled blocks of a task are in the past, mark it as done
        var now = DateTime.UtcNow;
        var existingBlocks =
            (await taskBlockRepository.GetByWorkProfileAsync(workProfileId, cancellationToken)).ToList();
        var blocksByTask = existingBlocks.GroupBy(b => b.TaskId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var task in allTasks.Where(t => t.Status != "done"))
            if (blocksByTask.TryGetValue(task.Id, out var taskBlocks)
                && taskBlocks.Count > 0
                && taskBlocks.All(b => b.EndDate <= now))
            {
                task.Status = "done";
                await taskRepository.UpdateAsync(task, cancellationToken);
            }

        var openTasks = allTasks.Where(t => t.Status != "done").ToList();

        if (openTasks.Count == 0)
            return new PlanningResult(true, null, 0, [], []);

        // Load work profile with day profiles, blocks and breaks
        var workProfile = await workProfileRepository.GetByIdAsync(workProfileId, cancellationToken);
        if (workProfile is null)
            return new PlanningResult(false, "Kein Arbeitsprofil gefunden.", 0, [], []);

        // Load blockings (appointments / busy intervals)
        var blockings = await workProfileRepository.GetTimeIntervalsAsync(workProfileId, cancellationToken);

        // Load recurring blockers
        var recurringBlockers =
            await recurringBlockerRepository.GetByWorkProfileAsync(workProfileId, cancellationToken);

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
        // Compute effective remaining duration (estimate minus past completed work) so the analyzer
        // does not over-estimate finish times for tasks that already have past blocks.
        var effectiveDurations = openTasks.ToDictionary(
            t => t.Id,
            t =>
            {
                var total = (int)t.TimeEstimate.TotalMinutes;
                if (!blocksByTask.TryGetValue(t.Id, out var pastBlocks))
                    return TimeSpan.FromMinutes(total);
                var alreadyDone = (int)pastBlocks
                    .Where(b => b.EndDate <= now)
                    .Sum(b => (b.EndDate - b.StartDate).TotalMinutes);
                return TimeSpan.FromMinutes(Math.Max(0, total - alreadyDone));
            });

        DependencyAnalysisResult analysis;
        try
        {
            analysis = dependencyAnalyzer.Analyze(openTasks, dependencies, projectStart, fixedTaskTimes,
                effectiveDurations);
        }
        catch (InvalidOperationException ex)
        {
            return new PlanningResult(false, ex.Message, 0, [], []);
        }

        // Generate free time slots from work profile.
        // Work-block strings ("09:00") are stored as the user's local wall-clock time
        // (see users.timezone DB column, default 'Europe/Berlin'). We resolve them against
        // that timezone and emit slots in UTC so all downstream comparisons stay timezone-safe.
        var userTz = ResolveUserTimezone(workProfile);
        var freeSlots = GenerateTimeSlots(workProfile, projectStart, projectEnd, userTz);

        // Ensure slots are sorted chronologically. GenerateTimeSlots produces them in order,
        // but explicit sorting guards against any edge-case (e.g., overlapping work blocks on the
        // same day). The scheduling algorithm relies on early-slot-first (EFS) ordering.
        freeSlots.Sort((a, b) => a.Start.CompareTo(b.Start));

        // Remove slots that are already in the past so tasks are never scheduled retroactively
        freeSlots.RemoveAll(s => s.End <= now);
        for (var i = 0; i < freeSlots.Count; i++)
            if (freeSlots[i].Start < now)
                freeSlots[i] = new TimeSlot(now, freeSlots[i].End, freeSlots[i].OrganizationId);

        // Remove blocking intervals from free slots
        foreach (var blocking in blockings)
            SubtractInterval(freeSlots, blocking.StartDate, blocking.EndDate);

        // Remove recurring blocker intervals from free slots
        foreach (var (start, end) in ExpandRecurringBlockers(recurringBlockers, projectStart, projectEnd, userTz))
            SubtractInterval(freeSlots, start, end);

        // Remove fixed task blocks from free slots so dynamic tasks cannot overlap them
        foreach (var fb in fixedBlocks)
            SubtractInterval(freeSlots, fb.StartDate, fb.EndDate);

        // Remove tiny slot fragments that are too short to be useful
        freeSlots.RemoveAll(s => s.DurationMinutes < greedyScheduler.MinBlockMinutes);

        // Initialize remaining durations for dynamically-scheduled tasks only,
        // reusing the effective durations already computed for the dependency analyzer.
        var remainingMinutes = tasksToSchedule.ToDictionary(
            t => t.Id,
            t => (int)effectiveDurations[t.Id].TotalMinutes);

        // Fixed tasks and done tasks count as scheduled for dependency resolution
        var alreadyScheduledIds = openTasks
            .Where(t => t.IsFixed || t.Status == "done")
            .Select(t => t.Id)
            .ToHashSet();

        // --- Greedy scheduling ---
        var originalRemaining = remainingMinutes.ToDictionary(kv => kv.Key, kv => kv.Value);

        var plannedBlocks = greedyScheduler.Schedule(
            tasksToSchedule, remainingMinutes, alreadyScheduledIds, freeSlots, analysis);

        // Deadline feasibility check: collect every task with a deadline that could not be fully scheduled.
        var infeasible = new List<string>();
        foreach (var task in tasksToSchedule.Where(t => t.Deadline.HasValue))
        {
            var needed = originalRemaining.GetValueOrDefault(task.Id, 0);
            if (needed <= 0) continue;

            var scheduled = plannedBlocks
                .Where(b => b.TaskId == task.Id)
                .Sum(b => (int)(b.EndDate - b.StartDate).TotalMinutes);

            if (scheduled < needed)
                infeasible.Add($"\"{task.Name}\" (Deadline {task.Deadline!.Value:dd.MM.yyyy HH:mm})");
        }

        if (infeasible.Count > 0)
        {
            // Do NOT overwrite the existing plan in the DB. Report which tasks could not be planned.
            var msg = "Folgende Aufgaben konnten nicht vor ihrer Deadline eingeplant werden: "
                      + string.Join(", ", infeasible);
            return new PlanningResult(false, msg, 0, plannedBlocks, []);
        }

        // Persist the plan and the per-task EarlyStart/Finish in a single transaction so a
        // failure mid-way never leaves the profile with new blocks but stale task fields
        // (or vice versa).
        await using var tx = await unitOfWork.BeginTransactionAsync(cancellationToken);

        await taskBlockRepository.ReplaceAsync(workProfileId, plannedBlocks, cancellationToken);

        // Sync EarlyStart / EarlyFinish on each task to its scheduled block window so the
        // frontend calendar can read the actual scheduled times from the task endpoint.
        var plannedBlocksByTask = plannedBlocks
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

        await tx.CommitAsync(cancellationToken);

        return new PlanningResult(true, null, 0, plannedBlocks, []);
    }

    // -------------------------------------------------------------------------
    // Time-slot generation
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Resolves the timezone for interpreting work-block wall-clock strings.
    ///     Falls back to Europe/Berlin (the DB default) and finally UTC if even that fails.
    /// </summary>
    private static TimeZoneInfo ResolveUserTimezone(WorkProfile workProfile)
    {
        var tzId = workProfile.Membership?.User?.Timezone;
        if (!string.IsNullOrWhiteSpace(tzId))
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            catch (TimeZoneNotFoundException)
            {
                /* fall through */
            }
            catch (InvalidTimeZoneException)
            {
                /* fall through */
            }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static List<TimeSlot> GenerateTimeSlots(
        WorkProfile workProfile, DateTime fromUtc, DateTime toUtc, TimeZoneInfo tz)
    {
        var slots = new List<TimeSlot>();
        var dayMap = workProfile.Days.ToDictionary(d => d.Day, StringComparer.OrdinalIgnoreCase);

        // Each work block is tagged with the company it belongs to (WorkBlock.CompanyId,
        // stored as the org's UUID-as-string). Slots inherit that tag so the scheduler can
        // match tasks to the correct org's shifts.
        var ownOrgId = workProfile.Membership?.OrganizationId;

        // Iterate over LOCAL calendar dates because work blocks are interpreted in the user's
        // local wall-clock time. UTC-day iteration would either miss or duplicate a day depending
        // on the user's offset (and is wrong at DST boundaries).
        var localFrom = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), tz).Date;
        var localTo = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc), tz).Date;

        for (var localDate = localFrom; localDate < localTo; localDate = localDate.AddDays(1))
        {
            var dayAbbrev = ToDayAbbreviation(localDate.DayOfWeek);
            if (!dayMap.TryGetValue(dayAbbrev, out var dayProfile))
                continue;

            var ownBlocks = dayProfile.Blocks.ToList();

            foreach (var block in ownBlocks)
            {
                var blockStart = ParseLocalTimeAsUtc(localDate, block.StartTime, tz);
                var blockEnd = ParseLocalTimeAsUtc(localDate, block.EndTime, tz);
                if (blockEnd <= blockStart) continue;

                // Resolve the block's org tag. Empty/unparsable CompanyId falls back to the
                // workprofile's own (personal) org so legacy blocks behave as before.
                var blockOrgId = ownOrgId;
                if (!string.IsNullOrWhiteSpace(block.CompanyId)
                    && Guid.TryParse(block.CompanyId, out var parsed))
                    blockOrgId = parsed;

                // Remove breaks that fall within this work block
                var breaks = dayProfile.Breaks
                    .Select(b => (
                        Start: ParseLocalTimeAsUtc(localDate, b.StartTime, tz),
                        End: ParseLocalTimeAsUtc(localDate, b.EndTime, tz)))
                    .Where(b => b.Start >= blockStart && b.End <= blockEnd && b.End > b.Start)
                    .OrderBy(b => b.Start)
                    .ToList();

                var cursor = blockStart;
                foreach (var brk in breaks)
                {
                    if (cursor < brk.Start)
                        slots.Add(new TimeSlot(cursor, brk.Start, blockOrgId));
                    cursor = brk.End;
                }

                if (cursor < blockEnd)
                    slots.Add(new TimeSlot(cursor, blockEnd, blockOrgId));
            }
        }

        return slots;
    }

    /// <summary>
    ///     Converts a wall-clock "HH:mm" string on the given local date into a UTC <see cref="DateTime" />
    ///     using the supplied timezone. Result has Kind=Utc so it compares safely with UtcNow and DB timestamps.
    /// </summary>
    private static DateTime ParseLocalTimeAsUtc(DateTime localDate, string hhMm, TimeZoneInfo tz)
    {
        var parts = hhMm.Split(':');
        var localDateTime = DateTime.SpecifyKind(
            localDate.Date.AddHours(int.Parse(parts[0])).AddMinutes(int.Parse(parts[1])),
            DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, tz);
    }

    private static IEnumerable<(DateTime Start, DateTime End)> ExpandRecurringBlockers(
        IReadOnlyList<RecurringBlocker> blockers, DateTime fromUtc, DateTime toUtc, TimeZoneInfo tz)
    {
        var localFrom = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), tz).Date;
        var localTo = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc), tz).Date;

        for (var localDate = localFrom; localDate < localTo; localDate = localDate.AddDays(1))
        {
            var dayAbbrev = ToDayAbbreviation(localDate.DayOfWeek);
            foreach (var blocker in blockers)
            {
                if (blocker.ValidFrom.HasValue && localDate < blocker.ValidFrom.Value.ToDateTime(TimeOnly.MinValue))
                    continue;
                if (blocker.ValidUntil.HasValue && localDate > blocker.ValidUntil.Value.ToDateTime(TimeOnly.MinValue))
                    continue;

                var days = blocker.DaysOfWeek.Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!days.Contains(dayAbbrev, StringComparer.OrdinalIgnoreCase))
                    continue;

                var start = ParseLocalTimeAsUtc(localDate, blocker.StartTime, tz);
                var end = ParseLocalTimeAsUtc(localDate, blocker.EndTime, tz);
                if (end > start)
                    yield return (start, end);
            }
        }
    }

    private static string ToDayAbbreviation(DayOfWeek day)
    {
        return day switch
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
    }

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
                slots.Insert(i, new TimeSlot(s.Start, busyStart, s.OrganizationId));
            if (busyEnd < s.End)
                slots.Insert(i + (s.Start < busyStart ? 1 : 0), new TimeSlot(busyEnd, s.End, s.OrganizationId));
        }
    }
}