using DataAccess.Models;

namespace Services.Planning;

/// <summary>
/// Greedy slot assignment: tasks are processed in dependency → deadline → priority order,
/// each filled into the earliest available free slots without backtracking.
/// </summary>
public class GreedyScheduler
{
    /// <summary>Minimum duration of a single work block in minutes. Slots shorter than this are skipped.</summary>
    public int MinBlockMinutes { get; init; } = 25;

    /// <summary>
    /// Maximum sustained focus duration per intensity level (minutes), based on:
    /// Ultradian rhythms / BRAC (Kleitman) → ~90 min, DeskTime study (2014) → 52 min,
    /// Pomodoro (Cirillo) → 25 min for highest intensity. Long focus blocks are split into
    /// multiple shorter blocks with breaks inserted in between.
    /// Light tasks are low cognitive load (admin work, routine) and need no enforced break,
    /// so the cap is effectively unlimited and the break is 0.
    /// </summary>
    public int LightMaxFocusMinutes { get; init; } = int.MaxValue;
    public int NormalMaxFocusMinutes { get; init; } = 90;
    public int IntensiveMaxFocusMinutes { get; init; } = 50;

    /// <summary>
    /// Recovery break (minutes) inserted after each placed block of this task — covers both
    /// intra-task pacing (same task continues after break) and inter-task context switching
    /// (next task starts after break). Light = 0 (no enforced break).
    /// Sources: BRAC recommends 15-20 min recovery between ultradian cycles; DeskTime → 17 min.
    /// </summary>
    public int LightBreakMinutes { get; init; } = 0;
    public int NormalBreakMinutes { get; init; } = 15;
    public int IntensiveBreakMinutes { get; init; } = 15;

    private int MaxFocusFor(ETaskIntensity intensity) => intensity switch
    {
        ETaskIntensity.Light => LightMaxFocusMinutes,
        ETaskIntensity.Intensive => IntensiveMaxFocusMinutes,
        _ => NormalMaxFocusMinutes,
    };

    private int BreakFor(ETaskIntensity intensity) => intensity switch
    {
        ETaskIntensity.Light => LightBreakMinutes,
        ETaskIntensity.Intensive => IntensiveBreakMinutes,
        _ => NormalBreakMinutes,
    };

    /// <summary>
    /// Schedules <paramref name="tasksToSchedule"/> greedily into <paramref name="freeSlots"/>.
    /// Tasks are processed one at a time in dependency → deadline → priority order.
    /// Each task is filled into the earliest available free slots until complete (no backtracking).
    /// </summary>
    /// <param name="tasksToSchedule">Dynamic (non-fixed) open tasks to schedule.</param>
    /// <param name="remainingMinutes">Minutes still needed per task (caller adjusts for partial past work).</param>
    /// <param name="alreadyScheduledIds">IDs that count as done for dependency resolution (fixed tasks, done tasks).</param>
    /// <param name="freeSlots">Available time slots sorted chronologically. Modified in place as slots are consumed.</param>
    /// <param name="analysis">Dependency graph produced by <see cref="DependencyAnalyzer"/>.</param>
    public List<TaskBlock> Schedule(
        IReadOnlyList<UserTask> tasksToSchedule,
        Dictionary<Guid, int> remainingMinutes,
        IReadOnlySet<Guid> alreadyScheduledIds,
        List<TimeSlot> freeSlots,
        DependencyAnalysisResult analysis)
    {
        var plannedBlocks = new List<TaskBlock>();
        var scheduledIds = new HashSet<Guid>(alreadyScheduledIds);
        // Tasks the greedy run touched but could not place *any* block for. They are removed
        // from `ready` (via remainingMinutes=0) but are NOT added to scheduledIds, so their
        // dependents remain blocked — otherwise the planner would happily place a successor
        // before its predecessor is actually done.
        var unschedulable = new HashSet<Guid>();

        while (true)
        {
            // Ready = all predecessors done, still has remaining work
            var ready = tasksToSchedule
                .Where(t => remainingMinutes.TryGetValue(t.Id, out var rem) && rem > 0
                            && !unschedulable.Contains(t.Id)
                            && AllPredecessorsDone(t.Id, analysis.Predecessors, scheduledIds))
                .ToList();

            if (ready.Count == 0) break;

            // Pick: earliest deadline first (null → far future), then highest priority, then oldest
            var task = ready
                .OrderBy(t => t.Deadline ?? DateTime.MaxValue)
                .ThenByDescending(t => (int)t.Priority)
                .ThenBy(t => t.CreatedAt)
                .First();

            var blocksPlacedThisTask = 0;

            // Fill this task greedily into the earliest available slots
            for (var i = 0; i < freeSlots.Count && remainingMinutes[task.Id] > 0;)
            {
                var slot = freeSlots[i];
                // Org constraint: a task tagged with an OrganizationId may only consume slots
                // tagged with the same org. Tasks without an org (legacy) accept any slot.
                if (task.OrganizationId.HasValue && slot.OrganizationId.HasValue
                    && task.OrganizationId.Value != slot.OrganizationId.Value)
                { i++; continue; }
                // The minimum useful block size is MinBlockMinutes, but tasks that are shorter
                // than that (or have less than that remaining) should still be schedulable.
                var minRequired = Math.Min(MinBlockMinutes, remainingMinutes[task.Id]);
                if (slot.DurationMinutes < minRequired) { i++; continue; }

                // Skip slots that start at or after the task's deadline (deadline is inclusive timestamp)
                if (task.Deadline.HasValue && slot.Start >= task.Deadline.Value)
                { i++; continue; }

                var blockMinutes = Math.Min(remainingMinutes[task.Id], slot.DurationMinutes);

                // Cap block to deadline boundary (deadline is the exact end timestamp, inclusive)
                if (task.Deadline.HasValue)
                {
                    var deadlineEnd = task.Deadline.Value;
                    blockMinutes = Math.Min(blockMinutes, (int)(deadlineEnd - slot.Start).TotalMinutes);
                }

                // Cap block to the intensity-specific max sustained focus duration. Anything
                // longer than this gets split into multiple blocks with breaks in between
                // (see break-insertion logic below).
                var maxFocus = MaxFocusFor(task.Intensity);
                blockMinutes = Math.Min(blockMinutes, maxFocus);

                if (blockMinutes < minRequired) { i++; continue; }

                var blockEnd = slot.Start.AddMinutes(blockMinutes);
                plannedBlocks.Add(new TaskBlock
                {
                    TaskId = task.Id,
                    StartDate = slot.Start,
                    EndDate = blockEnd,
                    IsFixed = false,
                    Task = task
                });
                remainingMinutes[task.Id] -= blockMinutes;
                blocksPlacedThisTask++;

                // Consume slot. Insert a recovery break of BreakFor(intensity) minutes after
                // every block — covers intra-task pacing AND inter-task context switching.
                // Light tasks have break=0 so no gap is inserted.
                if (blockEnd < slot.End)
                {
                    var taskHasMoreWork = remainingMinutes[task.Id] > 0;
                    var breakLen = BreakFor(task.Intensity);
                    var nextStart = blockEnd;

                    if (breakLen > 0)
                    {
                        var withBreak = blockEnd.AddMinutes(breakLen);
                        if (withBreak < slot.End
                            && (int)(slot.End - withBreak).TotalMinutes >= MinBlockMinutes)
                        {
                            nextStart = withBreak;
                        }
                        else if (taskHasMoreWork)
                        {
                            // Not enough room in this slot for break + a usable next block.
                            // Drop the rest of the slot so the task continues fresh in the
                            // next slot (which provides a natural break across day/shift).
                            freeSlots.RemoveAt(i);
                            continue;
                        }
                        // else: task done, no room for full break — drop the leftover so the
                        // next task in the queue doesn't start back-to-back without recovery.
                        else
                        {
                            freeSlots.RemoveAt(i);
                            continue;
                        }
                    }

                    freeSlots[i] = new TimeSlot(nextStart, slot.End, slot.OrganizationId);
                }
                else
                    freeSlots.RemoveAt(i); // i stays — next slot is now at index i
            }

            // Take this task out of the ready pool. If at least one block was placed we treat the
            // task as "done enough" to unblock its dependents (partial scheduling is allowed for
            // tasks without deadline). If nothing fit at all, mark it unschedulable so dependents
            // stay blocked — otherwise we'd schedule successors before their predecessor.
            remainingMinutes[task.Id] = 0;
            if (blocksPlacedThisTask > 0)
                scheduledIds.Add(task.Id);
            else
                unschedulable.Add(task.Id);
        }

        return plannedBlocks;
    }

    private static bool AllPredecessorsDone(
        Guid taskId,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> predecessors,
        HashSet<Guid> scheduled) =>
        !predecessors.TryGetValue(taskId, out var preds) || preds.All(scheduled.Contains);
}
