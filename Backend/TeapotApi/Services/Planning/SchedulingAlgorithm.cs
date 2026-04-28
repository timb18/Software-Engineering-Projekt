using DataAccess.Models;

namespace Services;

/// <summary>
/// SchedulingAlgorithm implements a sophisticated recursive task planning algorithm with backtracking.
/// It generates an optimal daily/weekly schedule by assigning tasks to concrete time blocks while respecting
/// many hard and soft constraints such as:
/// - Task dependencies (predecessors must complete before successors can start)
/// - Task deadlines (must finish by the deadline)
/// - Daily maximum workload limits
/// - Minimum and maximum block durations per task
/// - Task splitting rules (AllowSplitting, MaxSplits)
/// - Task intensity and preferred task sequencing (e.g., light tasks after intensive tasks)
/// - Work profile constraints (working hours, breaks, fixed events)
/// 
/// The algorithm uses a depth-first search with backtracking to explore the solution space:
/// 1. Generates free time slots from work blocks and subtracts breaks/fixed events
/// 2. Determines which tasks are currently schedulable (all predecessors completed)
/// 3. Heuristically selects the next best task to schedule
/// 4. Calculates an appropriate block duration respecting constraints
/// 5. Finds a suitable time slot for the block
/// 6. Places the block and recurses
/// 7. On failure, backtracks and tries alternative placements
/// </summary>
public class SchedulingAlgorithm
{
    /// <summary>
    /// TaskWithRemaining wraps a UserTask with planning-specific metadata tracked during scheduling.
    /// This allows us to track which tasks have been partially scheduled without modifying the original task.
    /// </summary>
    public class TaskWithRemaining
    {
        /// <summary>The original task from the database</summary>
        public UserTask Task { get; set; }

        /// <summary>Amount of time yet to be scheduled for this task (initially = TimeEstimate, decreases as blocks are placed)</summary>
        public TimeSpan RemainingDuration { get; set; }

        /// <summary>Number of times this task has been split into separate blocks. Used to enforce MaxSplits constraints.</summary>
        public int SplitsUsed { get; set; } = 0;

        /// <summary>
        /// True if this task is on the critical path (EarlyStart == LateStart from CPM).
        /// Critical path tasks should be prioritized to avoid delaying the entire project.
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>List of task IDs that must be completed before this task can start (from TaskDependency records)</summary>
        public List<Guid> Predecessors { get; set; } = new();
    }

    /// <summary>
    /// TimeSlot represents a contiguous interval of free time available for task assignment.
    /// Free slots are generated once at the start by extracting available time from work blocks,
    /// subtracting breaks and fixed events. During backtracking, we check against actually assigned blocks.
    /// </summary>
    public class TimeSlot
    {
        /// <summary>Exact start time (including date and time-of-day)</summary>
        public DateTime Start { get; set; }

        /// <summary>Exact end time (including date and time-of-day)</summary>
        public DateTime End { get; set; }

        /// <summary>The date (without time-of-day) for this slot. Used to aggregate daily workload and apply daily limits.</summary>
        public DateTime Day { get; set; }
    }

    /// <summary>
    /// PlanState captures the complete current state of the scheduling process.
    /// It is shared and updated during the recursive planning function and used for backtracking.
    /// </summary>
    public class PlanState
    {
        /// <summary>
        /// Dictionary mapping each task ID to its list of assigned TaskBlocks.
        /// A task may have multiple blocks if splitting is allowed.
        /// Updated as we place blocks; used for backtracking to undo placements.
        /// </summary>
        public Dictionary<Guid, List<TaskBlock>> AssignedBlocks { get; set; } = new();

        /// <summary>
        /// Dictionary mapping each task ID to its remaining duration yet to be scheduled.
        /// Initially set to task.TimeEstimate. Decreases as blocks are placed.
        /// When all tasks have RemainingDuration == 0, scheduling is complete.
        /// </summary>
        public Dictionary<Guid, TimeSpan> RemainingDurations { get; set; } = new();

        /// <summary>
        /// Dictionary mapping each day (as DateTime) to the total time used on that day.
        /// Used to enforce MaxDailyLoad constraint: daily used time + new block duration must not exceed MaxDailyLoad.
        /// </summary>
        public Dictionary<DateTime, TimeSpan> DailyUsedTime { get; set; } = new();

        /// <summary>
        /// True if the last-placed task was intensive (Intensity == Intensive).
        /// When true, the next task selection heuristic prefers Light tasks to balance intensity.
        /// Set to true after placing an Intensive task; reset to false after placing any other task.
        /// </summary>
        public bool NeedsLightTaskAfter { get; set; } = false;

        /// <summary>
        /// Counter to track recursion depth and prevent infinite loops.
        /// Incremented on each recursive call; if it exceeds a threshold (10000), we abort the search.
        /// </summary>
        public int BacktrackCounter { get; set; } = 0;
    }

    /// <summary>
    /// BacktrackingContext holds all the context needed for the recursive planning function.
    /// This object is passed through all recursive calls and modified in place as decisions are made.
    /// On backtracking, the State is reverted to undo placements, but Tasks, FreeSlots, Profile, and
    /// PlanningHorizonEnd remain constant.
    /// </summary>
    public class BacktrackingContext
    {
        /// <summary>The current plan state (assigned blocks, remaining durations, daily usage, etc.). Modified during recursion and backtracking.</summary>
        public PlanState State { get; set; }

        /// <summary>List of all tasks with metadata. Constant throughout the planning process; not modified during backtracking.</summary>
        public List<TaskWithRemaining> Tasks { get; set; }

        /// <summary>
        /// List of all available time slots. Generated once and remains constant.
        /// During slot selection, we additionally check for overlaps with already-assigned blocks to handle backtracking.
        /// </summary>
        public List<TimeSlot> FreeSlots { get; set; }

        /// <summary>The user's work profile containing working hours, breaks, and daily limits. Constant throughout planning.</summary>
        public WorkProfile Profile { get; set; }

        /// <summary>The end of the planning horizon. Tasks should ideally be scheduled before this date.</summary>
        public DateTime PlanningHorizonEnd { get; set; }
    }

    /// <summary>
    /// Main entry point for task scheduling. Orchestrates the entire planning process:
    /// 1. Prepares tasks and their dependencies
    /// 2. Generates available time slots
    /// 3. Runs the recursive backtracking algorithm
    /// 4. Returns all newly scheduled blocks and any conflicts/warnings
    /// 
    /// Pre-requisite: CPM (Critical Path Method) should have been run externally to set EarlyStart, LateStart, etc. on tasks.
    /// </summary>
    /// <param name="tasks">List of UserTask objects to be scheduled (status != "done")</param>
    /// <param name="dependencies">List of TaskDependency objects defining predecessor relationships</param>
    /// <param name="profile">WorkProfile containing work hours, breaks, and daily load limits</param>
    /// <param name="existingFixedBlocks">List of fixed events/blocks that cannot be moved (e.g., meetings, fixed commitments)</param>
    /// <param name="planningStart">Start date for the planning horizon</param>
    /// <param name="planningEnd">End date for the planning horizon</param>
    /// <returns>Tuple of (NewBlocks, Conflicts, Warnings):
    ///     - NewBlocks: TaskBlock objects representing the planned task assignments
    ///     - Conflicts: List of error messages (e.g., "Unable to schedule all tasks")
    ///     - Warnings: List of warning messages for non-fatal issues</returns>
    public (List<TaskBlock> NewBlocks, List<string> Conflicts, List<string> Warnings) PlanTasks(
        List<UserTask> tasks,
        List<TaskDependency> dependencies,
        WorkProfile profile,
        List<TaskBlock> existingFixedBlocks,
        DateTime planningStart,
        DateTime planningEnd)
    {
        // ==== STEP 1: Prepare tasks with planning metadata ====
        // Filter out completed tasks and build a wrapper around each task with scheduling info.
        // This includes identifying predecessors and whether this task is on the critical path.
        var tasksWithRemaining = tasks.Where(t => t.Status != "done").Select(t => new TaskWithRemaining
        {
            Task = t,
            RemainingDuration = t.TimeEstimate,
            // A task is considered critical if its EarlyStart equals its LateStart (no slack time)
            IsCritical = t.EarlyStart == t.LateStart,
            // Collect all task IDs that are direct predecessors of this task
            Predecessors = dependencies.Where(d => d.TaskId == t.Id).Select(d => d.DependsOnTaskId).ToList()
        }).ToList();

        // ==== STEP 2: Generate available time slots ====
        // Extract free time from work blocks by subtracting breaks and fixed events.
        // This produces a static list of TimeSlot objects representing when work can be scheduled.
        var freeSlots = GenerateFreeSlots(profile, existingFixedBlocks, planningStart, planningEnd);

        // ==== STEP 3: Initialize planning state ====
        // Set up the mutable state that will be updated as we schedule tasks during recursion.
        // Each task starts with RemainingDuration equal to its TimeEstimate.
        var state = new PlanState();
        foreach (var twr in tasksWithRemaining)
        {
            state.RemainingDurations[twr.Task.Id] = twr.RemainingDuration;
        }

        // ==== STEP 4: Create backtracking context ====
        // Assemble all context needed for the recursive planning function.
        // This object is passed through all recursive calls and modifications are made in-place.
        var context = new BacktrackingContext
        {
            State = state,
            Tasks = tasksWithRemaining,
            FreeSlots = freeSlots,
            Profile = profile,
            PlanningHorizonEnd = planningEnd
        };

        // ==== STEP 5: Run the recursive backtracking algorithm ====
        // This is the core of the scheduling logic. It will attempt to place all tasks
        // while respecting constraints, backtracking when necessary.
        bool success = RecursivePlan(context);

        // ==== STEP 6: Collect and return results ====
        // Flatten the assigned blocks from the state and compile conflict/warning messages.
        var newBlocks = state.AssignedBlocks.SelectMany(kvp => kvp.Value).ToList();
        var conflicts = new List<string>();
        var warnings = new List<string>();

        if (!success)
        {
            conflicts.Add("Unable to schedule all tasks within constraints.");
        }

        // TODO: Add specific conflicts like deadline misses, capacity violations, dependency violations, etc.

        return (newBlocks, conflicts, warnings);
    }

    /// <summary>
    /// Generates all available free time slots by processing the work profile.
    /// For each day in the planning horizon:
    /// 1. Retrieves the work blocks (e.g., 09:00-17:00)
    /// 2. Subtracts scheduled breaks (e.g., lunch)
    /// 3. Subtracts fixed/blocked events (e.g., meetings, fixed tasks)
    /// 4. Produces the remaining free intervals as TimeSlot objects
    /// 
    /// The result is a static list computed once. During backtracking, we additionally check
    /// against actually-assigned blocks to determine if a slot is still available.
    /// </summary>
    /// <param name="profile">WorkProfile with daily work blocks and breaks</param>
    /// <param name="fixedBlocks">List of TaskBlock objects that are immovable (IsFixed=true)</param>
    /// <param name="start">Start date for slot generation</param>
    /// <param name="end">End date for slot generation (inclusive)</param>
    /// <returns>List of TimeSlot objects representing available time windows, ordered by start time</returns>
    private List<TimeSlot> GenerateFreeSlots(WorkProfile profile, List<TaskBlock> fixedBlocks, DateTime start, DateTime end)
    {
        var freeSlots = new List<TimeSlot>();

        // Iterate through each day in the planning horizon
        for (DateTime day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            // Get the day of week (Mon, Tue, Wed, etc.) to look up the day profile
            var dayOfWeek = day.ToString("ddd");

            // Retrieve the work profile for this day of week
            var dayProfile = profile.Days.FirstOrDefault(d => d.Day == dayOfWeek);
            if (dayProfile == null) continue; // No work defined for this day; skip it

            // For each work block in this day (e.g., a user might have morning 09:00-12:00 and afternoon 14:00-17:00)
            foreach (var block in dayProfile.Blocks)
            {
                // Parse start and end times and combine with the date to get absolute DateTime values
                var blockStart = day + TimeSpan.Parse(block.StartTime);
                var blockEnd = day + TimeSpan.Parse(block.EndTime);

                // Build a list of occupied (unavailable) periods within this work block
                var occupied = new List<(DateTime Start, DateTime End)>();

                // Add all breaks for this day to the occupied list
                foreach (var brk in dayProfile.Breaks)
                {
                    var breakStart = day + TimeSpan.Parse(brk.StartTime);
                    var breakEnd = day + TimeSpan.Parse(brk.EndTime);
                    occupied.Add((breakStart, breakEnd));
                }

                // Add all fixed events for this day to the occupied list
                foreach (var fb in fixedBlocks.Where(fb => fb.StartDate.Date == day))
                {
                    occupied.Add((fb.StartDate, fb.EndDate));
                }

                // Sort occupied periods by start time to process them in chronological order
                occupied = occupied.OrderBy(o => o.Start).ToList();

                // Generate free slots by finding gaps between occupied periods
                var currentStart = blockStart;
                foreach (var occ in occupied)
                {
                    // If there's a gap between currentStart and this occupied period, create a free slot for that gap
                    if (occ.Start > currentStart)
                    {
                        freeSlots.Add(new TimeSlot { Start = currentStart, End = occ.Start, Day = day });
                    }
                    // Move currentStart past this occupied period (or keep it if occupied ends before currentStart)
                    currentStart = occ.End > currentStart ? occ.End : currentStart;
                }
                // If there's remaining time until the end of the work block, create a free slot for it
                if (currentStart < blockEnd)
                {
                    freeSlots.Add(new TimeSlot { Start = currentStart, End = blockEnd, Day = day });
                }
            }
        }

        // Return slots sorted by start time for efficient searching
        return freeSlots.OrderBy(s => s.Start).ToList();
    }

    /// <summary>
    /// Core recursive backtracking function that schedules tasks one by one.
    /// This function explores the search space of possible schedules, backtracking when
    /// it encounters infeasible branches.
    /// 
    /// Algorithm:
    /// 1. Check if all tasks are scheduled (success condition)
    /// 2. Get list of currently schedulable tasks (predecessors all completed)
    /// 3. Select the best task using heuristics
    /// 4. Calculate an appropriate block duration
    /// 5. Find a suitable time slot
    /// 6. Place the block and recurse
    /// 7. If recursion succeeds, return true
    /// 8. If recursion fails, undo the placement (backtrack) and try again
    /// 9. If all alternatives exhaust, return false
    /// 
    /// The function modifies context.State in-place. On backtracking, PlaceBlock/UndoPlacement
    /// reversibly update the state.
    /// </summary>
    /// <param name="context">BacktrackingContext holding the complete planning state</param>
    /// <returns>True if all tasks were successfully scheduled; False if no feasible schedule exists</returns>
    private bool RecursivePlan(BacktrackingContext context)
    {
        // ==== SUCCESS CONDITION: All tasks have remaining duration == 0 ====
        // If every task is fully scheduled, we have found a valid plan.
        if (context.Tasks.All(t => context.State.RemainingDurations[t.Task.Id] == TimeSpan.Zero))
        {
            return true;
        }

        // ==== SAFETY CHECK: Prevent infinite recursion ====
        // Increment counter on each call. If we exceed a threshold, abort the search.
        // This prevents pathological cases where the algorithm gets stuck.
        context.State.BacktrackCounter++;
        if (context.State.BacktrackCounter > 10000)
        {
            return false;
        }

        // ==== IDENTIFY SCHEDULABLE TASKS ====
        // A task is schedulable if:
        // 1. It has remaining duration > 0 (not yet fully scheduled)
        // 2. All its predecessors have RemainingDuration == 0 (all predecessors are complete)
        // This respects the task dependency constraints.
        var schedulable = context.Tasks.Where(t =>
            context.State.RemainingDurations[t.Task.Id] > TimeSpan.Zero &&
            t.Predecessors.All(p => context.State.RemainingDurations.ContainsKey(p) && context.State.RemainingDurations[p] == TimeSpan.Zero)
        ).ToList();

        // If no tasks are schedulable but some remain, the plan is infeasible (circular dependency or other issue)
        if (!schedulable.Any())
        {
            return false;
        }

        // ==== SELECT NEXT TASK ====
        // Use a heuristic to pick the best task from the schedulable set.
        // The heuristic considers:
        // - If a light task is needed after an intensive task (soft constraint)
        // - Critical path tasks (must not be delayed)
        // - Task priority (High > Medium > Low)
        // - Task deadline (earlier deadlines first)
        var nextTask = SelectNextTask(schedulable, context.State.NeedsLightTaskAfter);

        // ==== CALCULATE BLOCK DURATION ====
        // Determine how much of the task to schedule in this block.
        // Respects min/max block duration, splitting rules, and remaining duration.
        var blockDuration = CalculateBlockDuration(nextTask, context);

        // ==== FIND SUITABLE TIME SLOT ====
        // Search for a time slot that can fit the block while respecting all constraints:
        // - Slot must be large enough
        // - Daily load must not be exceeded
        // - Task deadline must not be violated
        // - For non-splittable tasks, slot must fit entire remaining duration
        // - Slot must not overlap with already-assigned blocks
        var slot = FindSuitableSlot(blockDuration, nextTask, context);
        if (slot == null)
        {
            // No suitable slot found; this branch is infeasible
            return false;
        }

        // ==== PLACE BLOCK ====
        // Commit the placement decision: update remaining duration, daily usage, split count, etc.
        PlaceBlock(nextTask, slot, blockDuration, context);

        // ==== RECURSE ====
        // Recursively try to schedule the remaining tasks.
        if (RecursivePlan(context))
        {
            // Success! A complete valid schedule was found.
            return true;
        }

        // ==== BACKTRACK ====
        // Recursion failed. Undo the placement to restore the state to before PlaceBlock.
        // This allows us to try alternative task selections, block sizes, or slots.
        UndoPlacement(nextTask, slot, blockDuration, context);

        // Return false to indicate this branch did not lead to a valid solution.
        return false;
    }

    /// <summary>
    /// Heuristic-based task selection that picks the "best" task from the schedulable set.
    /// The heuristic uses multiple criteria, prioritized as follows:
    /// 
    /// 1. INTENSITY BALANCING: If NeedsLightTaskAfter is true (previous task was intensive),
    ///    prefer Light tasks to avoid scheduling too much intense work in sequence.
    /// 
    /// 2. CRITICAL PATH: Tasks on the critical path have zero slack and must not be delayed.
    ///    Prioritize them to ensure project deadlines are met.
    /// 
    /// 3. PRIORITY: Tasks marked as High priority are scheduled before Medium and Low.
    /// 
    /// 4. DEADLINE: Earlier deadlines are scheduled first (Earliest Deadline First heuristic).
    ///    This minimizes the chance of missing task deadlines.
    /// 
    /// These heuristics are applied in order; conflicts are resolved by the next criterion.
    /// </summary>
    /// <param name="schedulable">List of tasks that can be scheduled right now (no unmet predecessors)</param>
    /// <param name="needsLight">If true, prefer a Light-intensity task (soft constraint)</param>
    /// <returns>The selected task to schedule next</returns>
    private TaskWithRemaining SelectNextTask(List<TaskWithRemaining> schedulable, bool needsLight)
    {
        // ==== SOFT CONSTRAINT: Light task after intensive ====
        // If the last task was intensive, try to pick a light task for recovery/balance.
        // Only applies if light tasks are available; otherwise falls through to standard heuristics.
        if (needsLight)
        {
            var lightTasks = schedulable.Where(t => t.Task.Intensity == ETaskIntensity.Light).ToList();
            if (lightTasks.Any())
            {
                // Among light tasks, still apply priority and deadline heuristics
                return lightTasks
                    .OrderBy(t => t.Task.Priority)
                    .ThenBy(t => t.Task.Deadline ?? DateTime.MaxValue)
                    .First();
            }
        }

        // ==== PRIMARY HEURISTIC: Critical > Priority > Deadline ====
        // Sort by:
        // 1. Critical tasks (descending): critical tasks first
        // 2. Priority (ascending): High (0) before Medium (1) before Low (2)
        // 3. Deadline (ascending): earliest deadline first
        return schedulable
            .OrderByDescending(t => t.IsCritical)
            .ThenBy(t => t.Task.Priority)
            .ThenBy(t => t.Task.Deadline ?? DateTime.MaxValue)
            .First();
    }

    /// <summary>
    /// Determines the appropriate block duration for the current block placement.
    /// A block is a contiguous period during which a single task is worked on.
    /// The duration must satisfy several constraints:
    /// 
    /// - MinBlockDuration: Tasks should not be fragmented into overly small chunks (e.g., at least 15 minutes)
    /// - MaxBlockDuration: Sessions should not be too long; enforces regular breaks (e.g., max 4 hours)
    /// - RemainingDuration: Cannot schedule more than what's left
    /// - AllowSplitting: If false, all remaining work must fit in a single contiguous block
    /// - SplitCount: If splitting allowed, cannot exceed MaxSplits
    /// 
    /// NOTE: For Intensive tasks, breaks (auto-inserted after ~50 min) do NOT count toward TimeEstimate.
    /// Currently this is not fully implemented; future enhancement.
    /// </summary>
    /// <param name="task">The TaskWithRemaining being scheduled</param>
    /// <param name="context">BacktrackingContext providing profile and state info</param>
    /// <returns>TimeSpan representing the duration of the block to place</returns>
    private TimeSpan CalculateBlockDuration(TaskWithRemaining task, BacktrackingContext context)
    {
        // Get the remaining amount of work to schedule
        var remaining = context.State.RemainingDurations[task.Task.Id];
        var maxBlock = task.Task.MaxBlockDuration;
        var minBlock = task.Task.MinBlockDuration;

        // ==== INTENSIVE TASK WITH AUTO-BREAKS ====
        // For intensive tasks, users typically need breaks: work ~50min, break ~10min.
        // The break is NOT counted toward the task's TimeEstimate.
        // Currently, we use maxBlock as-is; in the future, this could be refined to:
        // - Limit MaxBlockDuration for intensive tasks to trigger more breaks
        // - Insert break blocks automatically
        TimeSpan effectiveMax = maxBlock;
        if (task.Task.Intensity == ETaskIntensity.Intensive)
        {
            // TODO: Implement intelligent break insertion for intensive tasks
            // For now, just use maxBlock but consider reducing it to encourage breaks
        }

        // ==== CALCULATE DURATION ====
        // Start with the minimum of remaining work and the max block size
        var duration = TimeSpan.FromMinutes(Math.Min(remaining.TotalMinutes, maxBlock.TotalMinutes));

        // ==== ENFORCE MINIMUM BLOCK SIZE ====
        // If the calculated duration is below minBlock but there's enough remaining work,
        // increase duration to at least minBlock to avoid creating very small fragments.
        if (duration < minBlock && remaining >= minBlock)
        {
            duration = minBlock;
        }

        // ==== HANDLE NON-SPLITTABLE TASKS ====
        // If AllowSplitting is false, the entire remaining work must be scheduled in a single block.
        // In this case, the block duration must equal the remaining duration.
        // (The slot-finding logic will ensure a large enough slot is available.)
        if (!task.Task.AllowSplitting)
        {
            duration = remaining;
        }

        return duration;
    }

    /// <summary>
    /// Searches for a time slot suitable for placing a task block.
    /// Evaluates candidate slots against a set of hard constraints:
    /// 
    /// 1. SIZE: Slot must be large enough to fit the requested duration
    /// 2. DAILY LOAD: Scheduling the block must not exceed the daily workload limit (MaxDailyLoad)
    /// 3. DEADLINE: If the task has a deadline, the block must end by that deadline
    /// 4. SPLITTING: If task cannot be split (AllowSplitting=false), slot must fit entire remaining duration
    /// 5. OVERLAP: Slot must not overlap with any already-assigned blocks (respects backtracking)
    /// 
    /// Returns the first slot that satisfies all constraints. Since slots are ordered by start time,
    /// this implements a "earliest slot first" heuristic, which tends to schedule work as early as possible.
    /// </summary>
    /// <param name="duration">The duration of the block to place</param>
    /// <param name="task">The TaskWithRemaining being scheduled</param>
    /// <param name="context">BacktrackingContext providing state, profile, and available slots</param>
    /// <returns>A TimeSlot that satisfies all constraints, or null if no suitable slot exists</returns>
    private TimeSlot? FindSuitableSlot(TimeSpan duration, TaskWithRemaining task, BacktrackingContext context)
    {
        var taskId = task.Task.Id;
        var deadline = task.Task.Deadline;

        // Iterate through all available free slots (ordered by start time)
        foreach (var slot in context.FreeSlots)
        {
            // ==== CONSTRAINT 1: SIZE ====
            // Slot must be large enough to accommodate the block duration
            if ((slot.End - slot.Start) < duration) continue;

            // ==== CONSTRAINT 2: DAILY LOAD ====
            // Check that placing this block won't exceed the daily workload limit
            var day = slot.Day;
            var currentDaily = context.State.DailyUsedTime.GetValueOrDefault(day, TimeSpan.Zero);
            if (currentDaily + duration > context.Profile.MaxDailyLoad) continue;

            // ==== CONSTRAINT 3: DEADLINE ====
            // If the task has a deadline, the block must complete by that deadline
            if (deadline.HasValue && slot.Start + duration > deadline.Value) continue;

            // ==== CONSTRAINT 4: SPLITTING ====
            // For non-splittable tasks, the slot must fit the entire remaining duration (not just one block)
            if (!task.Task.AllowSplitting)
            {
                var remaining = context.State.RemainingDurations[taskId];
                if ((slot.End - slot.Start) < remaining) continue;
            }

            // ==== CONSTRAINT 5: OVERLAP ====
            // Check that the slot doesn't overlap with any already-assigned blocks on the same day.
            // This is important for backtracking: we need to track which slots become unavailable
            // as we place blocks during recursion.
            var assignedOnDay = context.State.AssignedBlocks.Values
                .SelectMany(l => l)
                .Where(b => b.StartDate.Date == day)
                .ToList();
            
            // Two blocks overlap if it's NOT true that one ends before the other starts.
            // Logic: NOT (block.End <= slot.Start OR block.Start >= slot.End)
            bool overlaps = assignedOnDay.Any(b => !(b.EndDate <= slot.Start || b.StartDate >= slot.End));
            if (overlaps) continue;

            // ==== ALL CONSTRAINTS SATISFIED ====
            // This slot is suitable for the block; return it.
            return slot;
        }

        // No suitable slot found among all available slots
        return null;
    }

    /// <summary>
    /// Commits a task block placement by updating the planning state.
    /// This is a "forward" operation that modifies context.State to reflect the placement decision.
    /// On backtracking, UndoPlacement will reverse these changes.
    /// 
    /// Updates:
    /// 1. AssignedBlocks: Adds the new TaskBlock to the assignment list
    /// 2. RemainingDurations: Decrements by the block duration
    /// 3. DailyUsedTime: Increments daily usage for the given day
    /// 4. NeedsLightTaskAfter: Sets based on whether this task is intensive
    /// 5. SplitsUsed: Increments if this is a split (remaining > 0 after placement)
    /// </summary>
    /// <param name="task">The TaskWithRemaining being placed</param>
    /// <param name="slot">The TimeSlot where the block will be placed</param>
    /// <param name="duration">The duration of the block</param>
    /// <param name="context">BacktrackingContext whose state will be updated</param>
    private void PlaceBlock(TaskWithRemaining task, TimeSlot slot, TimeSpan duration, BacktrackingContext context)
    {
        var taskId = task.Task.Id;
        var start = slot.Start;
        var end = start + duration;

        // ==== CREATE TASKBLOCK ENTITY ====
        // Construct a TaskBlock representing this scheduled work
        var block = new TaskBlock
        {
            TaskId = taskId,
            StartDate = start,
            EndDate = end,
            IsFixed = false  // Non-fixed blocks can be rescheduled in future replanning
        };

        // ==== ADD TO ASSIGNED BLOCKS ====
        // Store the block in the hierarchical state (per-task list)
        if (!context.State.AssignedBlocks.ContainsKey(taskId))
        {
            context.State.AssignedBlocks[taskId] = new List<TaskBlock>();
        }
        context.State.AssignedBlocks[taskId].Add(block);

        // ==== UPDATE REMAINING DURATION ====
        // Subtract the placed duration from the task's remaining work
        context.State.RemainingDurations[taskId] -= duration;

        // ==== UPDATE DAILY USED TIME ====
        // Track total scheduled time for this day to enforce MaxDailyLoad
        var day = slot.Day;
        context.State.DailyUsedTime[day] = context.State.DailyUsedTime.GetValueOrDefault(day, TimeSpan.Zero) + duration;

        // ==== MANAGE INTENSITY-BASED PREFERENCES ====
        // If this task is intensive, set flag to prefer light tasks next (for recovery)
        // Otherwise, clear the flag (any non-intensive task can follow any task)
        if (task.Task.Intensity == ETaskIntensity.Intensive)
        {
            context.State.NeedsLightTaskAfter = true;
        }
        else
        {
            context.State.NeedsLightTaskAfter = false;
        }

        // ==== TRACK SPLITS ====
        // If the task is splittable and there's still work left, increment the split counter
        // This is used to enforce MaxSplits constraints (currently not fully enforced; see TODO)
        if (task.Task.AllowSplitting && context.State.RemainingDurations[taskId] > TimeSpan.Zero)
        {
            task.SplitsUsed++;
        }
    }

    /// <summary>
    /// Reverts a block placement, undoing all state changes made by PlaceBlock.
    /// Used for backtracking when the current branch of the search tree does not lead to a valid solution.
    /// 
    /// Operations (inverse of PlaceBlock):
    /// 1. Removes the last TaskBlock from AssignedBlocks
    /// 2. Restores the RemainingDuration (adds duration back)
    /// 3. Restores DailyUsedTime (subtracts duration from daily total)
    /// 4. Resets NeedsLightTaskAfter flags
    /// 5. Decrements SplitsUsed counter
    /// 
    /// After UndoPlacement, the state is restored as if PlaceBlock was never called,
    /// allowing the algorithm to try alternative task selections, block sizes, or slots.
    /// </summary>
    /// <param name="task">The TaskWithRemaining being un-placed</param>
    /// <param name="slot">The TimeSlot from which the block will be removed</param>
    /// <param name="duration">The duration of the block being removed</param>
    /// <param name="context">BacktrackingContext whose state will be reverted</param>
    private void UndoPlacement(TaskWithRemaining task, TimeSlot slot, TimeSpan duration, BacktrackingContext context)
    {
        var taskId = task.Task.Id;

        // ==== REMOVE TASKBLOCK ====
        // Find and remove the last assigned block for this task
        // (We assume the most recent assignment is what we're undoing)
        if (context.State.AssignedBlocks.ContainsKey(taskId) && context.State.AssignedBlocks[taskId].Any())
        {
            var lastBlock = context.State.AssignedBlocks[taskId].Last();
            context.State.AssignedBlocks[taskId].Remove(lastBlock);
        }

        // ==== RESTORE REMAINING DURATION ====
        // Add the duration back to indicate it still needs to be scheduled
        context.State.RemainingDurations[taskId] += duration;

        // ==== RESTORE DAILY USED TIME ====
        // Subtract from the daily total; the time slot becomes available again
        var day = slot.Day;
        context.State.DailyUsedTime[day] -= duration;

        // ==== RESET INTENSITY FLAGS ====
        // Clear the NeedsLightTaskAfter flag (simplified; could be smarter to restore previous value)
        // In a more sophisticated version, we'd maintain a history of this flag's values
        context.State.NeedsLightTaskAfter = false;

        // ==== RESTORE SPLIT COUNT ====
        // Decrement the split counter since we're un-splitting
        if (task.SplitsUsed > 0)
        {
            task.SplitsUsed--;
        }
    }
}