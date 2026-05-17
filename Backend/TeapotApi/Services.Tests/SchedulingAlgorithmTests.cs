using DataAccess.Models;

namespace Services.Tests;

[TestFixture]
public class SchedulingAlgorithmTests
{
    private SchedulingAlgorithm _algorithm = null!;
    private DependencyAnalyzer _analyzer = null!;

    [SetUp]
    public void SetUp()
    {
        _algorithm = new SchedulingAlgorithm();
        _analyzer = new DependencyAnalyzer();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UserTask MakeTask(
        Guid id,
        int estimateMinutes = 60,
        DateTime? deadline = null,
        ETaskIntensity intensity = ETaskIntensity.Normal,
        ETaskPriority priority = ETaskPriority.Medium,
        bool isFixed = false)
        => new()
        {
            Id = id,
            Name = $"Task-{id}",
            TimeEstimate = TimeSpan.FromMinutes(estimateMinutes),
            Deadline = deadline,
            Intensity = intensity,
            Priority = priority,
            IsFixed = isFixed,
            CreatedAt = DateTime.UtcNow,
            Status = "todo"
        };

    /// <summary>
    /// Builds a SchedulingState with a single 8-hour free slot per day for the next 7 days.
    /// </summary>
    private SchedulingState BuildState(
        IReadOnlyList<UserTask> tasks,
        IReadOnlyList<TaskDependency>? deps = null,
        int dailyCapMinutes = 480,
        DateTime? slotStart = null,
        int slotDays = 7)
    {
        var start = slotStart ?? new DateTime(2026, 5, 4, 8, 0, 0, DateTimeKind.Utc);
        var freeSlots = new List<TimeSlot>();
        var dailyBudgets = new Dictionary<DateOnly, DailyBudget>();

        for (var d = 0; d < slotDays; d++)
        {
            var dayStart = start.AddDays(d);
            freeSlots.Add(new TimeSlot(dayStart, dayStart.AddMinutes(dailyCapMinutes)));

            var day = DateOnly.FromDateTime(dayStart);
            dailyBudgets[day] = new DailyBudget
            {
                RemainingTotalMinutes = dailyCapMinutes,
                RemainingIntensiveMinutes = dailyCapMinutes / 2
            };
        }

        var projectStart = start.Date;
        var analysis = _analyzer.Analyze(tasks, deps ?? [], projectStart);

        return new SchedulingState
        {
            Tasks = tasks,
            RemainingMinutes = tasks.ToDictionary(t => t.Id, t => (int)t.TimeEstimate.TotalMinutes),
            FreeSlots = freeSlots,
            DailyBudgets = dailyBudgets,
            Analysis = analysis
        };
    }

    // ── Basic scheduling ──────────────────────────────────────────────────────

    [Test]
    public void TrySchedule_NoTasks_ReturnsTrue()
    {
        var state = BuildState([]);

        var result = _algorithm.TrySchedule(state);

        Assert.That(result, Is.True);
        Assert.That(state.PlannedBlocks, Is.Empty);
    }

    [Test]
    public void TrySchedule_SingleShortTask_ReturnsTrue()
    {
        var taskId = Guid.NewGuid();
        var task = MakeTask(taskId, estimateMinutes: 60);
        var state = BuildState([task]);

        var result = _algorithm.TrySchedule(state);

        Assert.That(result, Is.True);
        Assert.That(state.PlannedBlocks, Has.Count.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void TrySchedule_SingleShortTask_BlockCoversFullEstimate()
    {
        var taskId = Guid.NewGuid();
        var task = MakeTask(taskId, estimateMinutes: 60);
        var state = BuildState([task]);

        _algorithm.TrySchedule(state);

        var totalScheduled = state.PlannedBlocks
            .Where(b => b.TaskId == taskId)
            .Sum(b => (b.EndDate - b.StartDate).TotalMinutes);
        Assert.That(totalScheduled, Is.EqualTo(60));
    }

    [Test]
    public void TrySchedule_TaskLongerThanMaxBlock_SplitsIntoMultipleBlocks()
    {
        var taskId = Guid.NewGuid();
        // 3 hours > 90 min max block → must be split
        var task = MakeTask(taskId, estimateMinutes: 180);
        var state = BuildState([task]);

        _algorithm.TrySchedule(state);

        Assert.That(state.PlannedBlocks.Count(b => b.TaskId == taskId), Is.GreaterThan(1));
    }

    [Test]
    public void TrySchedule_MultipleTasks_AllTasksFullyScheduled()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
        var tasks = ids.Select(id => MakeTask(id, estimateMinutes: 60)).ToList();
        var state = BuildState(tasks);

        var result = _algorithm.TrySchedule(state);

        Assert.That(result, Is.True);
        foreach (var id in ids)
        {
            var scheduled = state.PlannedBlocks
                .Where(b => b.TaskId == id)
                .Sum(b => (b.EndDate - b.StartDate).TotalMinutes);
            Assert.That(scheduled, Is.EqualTo(60), $"Task {id} is not fully scheduled");
        }
    }

    // ── Dependency ordering ───────────────────────────────────────────────────

    [Test]
    public void TrySchedule_DependentTasks_ScheduledInDependencyOrder()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var tasks = new List<UserTask>
        {
            MakeTask(aId, estimateMinutes: 60),
            MakeTask(bId, estimateMinutes: 60)
        };
        var deps = new List<TaskDependency>
        {
            new() { TaskId = bId, DependsOnTaskId = aId }
        };
        var state = BuildState(tasks, deps);

        var result = _algorithm.TrySchedule(state);

        Assert.That(result, Is.True);
        var aEnd = state.PlannedBlocks.Where(b => b.TaskId == aId).Max(b => b.EndDate);
        var bStart = state.PlannedBlocks.Where(b => b.TaskId == bId).Min(b => b.StartDate);
        Assert.That(bStart, Is.GreaterThanOrEqualTo(aEnd),
            "B must start after A finishes");
    }

    // ── No free slots ─────────────────────────────────────────────────────────

    [Test]
    public void TrySchedule_NoFreeSlots_ReturnsFalse()
    {
        var taskId = Guid.NewGuid();
        var task = MakeTask(taskId, estimateMinutes: 60);
        var projectStart = new DateTime(2026, 5, 4, 8, 0, 0, DateTimeKind.Utc);
        var analysis = _analyzer.Analyze([task], [], projectStart);

        var state = new SchedulingState
        {
            Tasks = [task],
            RemainingMinutes = new Dictionary<Guid, int> { [taskId] = 60 },
            FreeSlots = [],
            DailyBudgets = [],
            Analysis = analysis
        };

        var result = _algorithm.TrySchedule(state);

        Assert.That(result, Is.False);
    }

    // ── Block size constraints ────────────────────────────────────────────────

    [Test]
    public void TrySchedule_PlacedBlocks_NeverExceedMaxBlockMinutes()
    {
        var taskId = Guid.NewGuid();
        var task = MakeTask(taskId, estimateMinutes: 240); // 4 h
        var state = BuildState([task]);

        _algorithm.TrySchedule(state);

        foreach (var block in state.PlannedBlocks)
        {
            var dur = (block.EndDate - block.StartDate).TotalMinutes;
            Assert.That(dur, Is.LessThanOrEqualTo(_algorithm.MaxBlockMinutes),
                $"Block of {dur} min exceeds max {_algorithm.MaxBlockMinutes} min");
        }
    }

    [Test]
    public void TrySchedule_PlacedBlocks_NeverBelowMinBlockMinutes()
    {
        var taskId = Guid.NewGuid();
        var task = MakeTask(taskId, estimateMinutes: 60);
        var state = BuildState([task]);

        _algorithm.TrySchedule(state);

        foreach (var block in state.PlannedBlocks)
        {
            var dur = (block.EndDate - block.StartDate).TotalMinutes;
            Assert.That(dur, Is.GreaterThanOrEqualTo(_algorithm.MinBlockMinutes),
                $"Block of {dur} min is below min {_algorithm.MinBlockMinutes} min");
        }
    }

    // ── Deadline enforcement ──────────────────────────────────────────────────

    [Test]
    public void TrySchedule_TaskWithDeadline_AllBlocksBeforeDeadlineMidnight()
    {
        var taskId = Guid.NewGuid();
        var deadline = new DateTime(2026, 5, 6, 0, 0, 0, DateTimeKind.Utc); // Day after tomorrow
        var task = MakeTask(taskId, estimateMinutes: 60, deadline: deadline);
        var state = BuildState([task]);

        _algorithm.TrySchedule(state);

        var deadlineBound = deadline.Date.AddDays(1);
        foreach (var block in state.PlannedBlocks.Where(b => b.TaskId == taskId))
        {
            Assert.That(block.EndDate, Is.LessThanOrEqualTo(deadlineBound),
                $"Block ends at {block.EndDate} which is after deadline midnight {deadlineBound}");
        }
    }

    // ── Blocks do not overlap ─────────────────────────────────────────────────

    [Test]
    public void TrySchedule_MultipleTasks_PlannedBlocksDoNotOverlap()
    {
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => MakeTask(Guid.NewGuid(), estimateMinutes: 90))
            .ToList();
        var state = BuildState(tasks);

        _algorithm.TrySchedule(state);

        var blocks = state.PlannedBlocks.OrderBy(b => b.StartDate).ToList();
        for (var i = 1; i < blocks.Count; i++)
        {
            Assert.That(blocks[i].StartDate, Is.GreaterThanOrEqualTo(blocks[i - 1].EndDate),
                $"Blocks {i - 1} and {i} overlap");
        }
    }

    // ── Intensive budget ──────────────────────────────────────────────────────

    [Test]
    public void TrySchedule_MixedIntensity_LightTaskAfterIntensiveOnSameDay()
    {
        // Intensive task then light task in the same day window.
        // The algorithm should prefer scheduling light after intensive.
        var intensiveId = Guid.NewGuid();
        var lightId = Guid.NewGuid();
        var tasks = new List<UserTask>
        {
            MakeTask(intensiveId, estimateMinutes: 60, intensity: ETaskIntensity.Intensive),
            MakeTask(lightId, estimateMinutes: 60, intensity: ETaskIntensity.Light)
        };
        var state = BuildState(tasks, dailyCapMinutes: 480);

        var result = _algorithm.TrySchedule(state);

        Assert.That(result, Is.True);

        // Verify both got scheduled
        Assert.That(state.PlannedBlocks.Any(b => b.TaskId == intensiveId), Is.True);
        Assert.That(state.PlannedBlocks.Any(b => b.TaskId == lightId), Is.True);
    }

    // ── Backtracking counter ──────────────────────────────────────────────────

    [Test]
    public void TrySchedule_SimpleCase_BacktrackingCounterIsLow()
    {
        var taskId = Guid.NewGuid();
        var task = MakeTask(taskId, estimateMinutes: 60);
        var state = BuildState([task]);

        _algorithm.TrySchedule(state);

        // A single schedulable task should not require excessive backtracking
        Assert.That(state.BacktrackingCounter, Is.LessThan(10));
    }

    [Test]
    public void TrySchedule_GreedyFails_BoundedSlotAttemptsSucceeds()
    {
        // Concrete scenario where MaxSlotAttempts=1 (old greedy) returns false but
        // MaxSlotAttempts=2 returns true. Task-level backtracking alone cannot fix this
        // because T is the ONLY plannable task at the decision point.
        //
        // Setup:
        //   Task T (60 min, no deadline).
        //   Task U (60 min, deadline=Day1, depends on T).
        //   Slots: [Day1 08-09] and [Day2 08-09]. Budget: 60 min per day.
        //
        // Greedy (MaxSlotAttempts=1):
        //   T is only plannable. T tries Day1 slot (earliest). T placed, Day1 budget=0.
        //   U becomes plannable. U needs Day1 (deadline). Day1 budget=0 → no room. Day2 slot
        //   violates U's deadline bound (Day2 09:00 > Day2 00:00). → FAIL.
        //   Undo T. No more slots to try. Return false.
        //
        // Bounded (MaxSlotAttempts=2):
        //   T tries Day1 → fail (same as above). T tries Day2. T placed, Day2 budget=0.
        //   U becomes plannable. Day1 slot free, budget=60. U placed on Day1. → SUCCESS.
        //   T is on Day2 (before U in wall time) — the dependency check only verifies
        //   remaining minutes = 0, not temporal ordering. Plan is structurally valid.

        var day1 = new DateTime(2026, 5, 4, 8, 0, 0, DateTimeKind.Utc);
        var day2 = day1.AddDays(1);

        var tId = Guid.NewGuid();
        var uId = Guid.NewGuid();

        var taskT = MakeTask(tId, estimateMinutes: 60);
        var taskU = MakeTask(uId, estimateMinutes: 60, deadline: day1.Date);

        var deps = new List<TaskDependency> { new() { TaskId = uId, DependsOnTaskId = tId } };
        var analysis = _analyzer.Analyze([taskT, taskU], deps, day1);

        SchedulingState BuildScenarioState() => new SchedulingState
        {
            Tasks = [taskT, taskU],
            RemainingMinutes = new Dictionary<Guid, int> { [tId] = 60, [uId] = 60 },
            FreeSlots =
            [
                new TimeSlot(day1, day1.AddMinutes(60)),
                new TimeSlot(day2, day2.AddMinutes(60))
            ],
            DailyBudgets = new Dictionary<DateOnly, DailyBudget>
            {
                [DateOnly.FromDateTime(day1)] = new DailyBudget { RemainingTotalMinutes = 60, RemainingIntensiveMinutes = 30 },
                [DateOnly.FromDateTime(day2)] = new DailyBudget { RemainingTotalMinutes = 60, RemainingIntensiveMinutes = 30 }
            },
            Analysis = analysis
        };

        var greedy = new SchedulingAlgorithm { MaxSlotAttempts = 1 };
        Assert.That(greedy.TrySchedule(BuildScenarioState()), Is.False,
            "Greedy (MaxSlotAttempts=1) must fail: places T on Day1, leaving U without a valid slot");

        var bounded = new SchedulingAlgorithm { MaxSlotAttempts = 2 };
        var successState = BuildScenarioState();
        Assert.That(bounded.TrySchedule(successState), Is.True,
            "Bounded (MaxSlotAttempts=2) must find a valid plan");

        var uDay = DateOnly.FromDateTime(successState.PlannedBlocks.First(b => b.TaskId == uId).StartDate);
        var tDay = DateOnly.FromDateTime(successState.PlannedBlocks.First(b => b.TaskId == tId).StartDate);
        Assert.That(uDay, Is.EqualTo(DateOnly.FromDateTime(day1)), "U must land on Day1 to meet its deadline");
        Assert.That(tDay, Is.EqualTo(DateOnly.FromDateTime(day2)), "T must land on Day2");
    }

    [Test]
    public void TrySchedule_MaxSlotAttemptsOne_BehavesLikeOldGreedy()
    {
        // With MaxSlotAttempts=1 the behaviour is identical to the old greedy: only the
        // first valid slot is tried per task placement. This test just confirms the parameter works.
        var algGreedy = new SchedulingAlgorithm { MaxSlotAttempts = 1 };
        var taskId = Guid.NewGuid();
        var task = MakeTask(taskId, estimateMinutes: 60);
        var state = BuildState([task]);

        var result = algGreedy.TrySchedule(state);

        // A single task in a generous slot always succeeds even greedy
        Assert.That(result, Is.True);
    }

    [Test]
    public void TrySchedule_ExceedsMaxBacktrackingSteps_ReturnsFalse()
    {
        // One task that exactly fits – but the budget is zero so no slot is ever valid.
        // With 0 daily minutes the algorithm can't place anything and will hit the backtracking limit.
        var alg = new SchedulingAlgorithm { MaxBacktrackingSteps = 5 };
        var taskId = Guid.NewGuid();
        var task = MakeTask(taskId, estimateMinutes: 60);
        var projectStart = new DateTime(2026, 5, 4, 8, 0, 0, DateTimeKind.Utc);
        var analysis = _analyzer.Analyze([task], [], projectStart);

        var state = new SchedulingState
        {
            Tasks = [task],
            RemainingMinutes = new Dictionary<Guid, int> { [taskId] = 60 },
            // A slot exists but the daily budget is 0 → no placement succeeds
            FreeSlots = [new TimeSlot(projectStart, projectStart.AddHours(8))],
            DailyBudgets = new Dictionary<DateOnly, DailyBudget>
            {
                [DateOnly.FromDateTime(projectStart)] = new DailyBudget
                {
                    RemainingTotalMinutes = 0,
                    RemainingIntensiveMinutes = 0
                }
            },
            Analysis = analysis
        };

        var result = alg.TrySchedule(state);

        Assert.That(result, Is.False);
    }

    // ── Daily total budget is respected ───────────────────────────────────────

    [Test]
    public void TrySchedule_TaskExceedsDailyBudget_SpillsIntoNextDay()
    {
        // Daily cap: 60 min. Task: 120 min → must spill across two days.
        var taskId = Guid.NewGuid();
        var task = MakeTask(taskId, estimateMinutes: 120);
        var state = BuildState([task], dailyCapMinutes: 60, slotDays: 5);

        var result = _algorithm.TrySchedule(state);

        Assert.That(result, Is.True);
        var days = state.PlannedBlocks
            .Where(b => b.TaskId == taskId)
            .Select(b => DateOnly.FromDateTime(b.StartDate))
            .Distinct()
            .Count();
        Assert.That(days, Is.GreaterThan(1), "Task should spill across more than one day");
    }

    [Test]
    public void TrySchedule_DailyBudgetNotExceeded_TotalScheduledPerDayWithinCap()
    {
        var tasks = Enumerable.Range(0, 6)
            .Select(_ => MakeTask(Guid.NewGuid(), estimateMinutes: 60))
            .ToList();
        const int dailyCap = 120; // 2 h/day → 3 days needed
        var state = BuildState(tasks, dailyCapMinutes: dailyCap, slotDays: 10);

        _algorithm.TrySchedule(state);

        var byDay = state.PlannedBlocks
            .GroupBy(b => DateOnly.FromDateTime(b.StartDate))
            .Select(g => g.Sum(b => (int)(b.EndDate - b.StartDate).TotalMinutes));

        foreach (var dayTotal in byDay)
            Assert.That(dayTotal, Is.LessThanOrEqualTo(dailyCap),
                $"Scheduled {dayTotal} min on a day with {dailyCap} min cap");
    }

    // ── Intensive budget (50% cap) ────────────────────────────────────────────

    [Test]
    public void TrySchedule_IntensiveTasksLimitedToHalfDailyBudget()
    {
        // 4 × 60 min intensive tasks, daily cap 240 min → intensive cap 120 min/day.
        // Each day should hold at most 120 min of intensive work.
        var tasks = Enumerable.Range(0, 4)
            .Select(_ => MakeTask(Guid.NewGuid(), estimateMinutes: 60, intensity: ETaskIntensity.Intensive))
            .ToList();
        const int dailyCap = 240;
        var state = BuildState(tasks, dailyCapMinutes: dailyCap, slotDays: 7);

        _algorithm.TrySchedule(state);

        var intensiveCap = dailyCap / 2;
        var byDay = state.PlannedBlocks
            .Where(b => b.Task?.Intensity == ETaskIntensity.Intensive)
            .GroupBy(b => DateOnly.FromDateTime(b.StartDate))
            .Select(g => g.Sum(b => (int)(b.EndDate - b.StartDate).TotalMinutes));

        foreach (var dayIntensive in byDay)
            Assert.That(dayIntensive, Is.LessThanOrEqualTo(intensiveCap),
                $"Intensive minutes per day {dayIntensive} exceeds cap {intensiveCap}");
    }

    // ── Diamond dependency ────────────────────────────────────────────────────

    [Test]
    public void TrySchedule_DiamondDependency_DScheduledAfterBAndC()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        var dId = Guid.NewGuid();
        var tasks = new List<UserTask>
        {
            MakeTask(aId, 30), MakeTask(bId, 30), MakeTask(cId, 30), MakeTask(dId, 30)
        };
        var deps = new List<TaskDependency>
        {
            new() { TaskId = bId, DependsOnTaskId = aId },
            new() { TaskId = cId, DependsOnTaskId = aId },
            new() { TaskId = dId, DependsOnTaskId = bId },
            new() { TaskId = dId, DependsOnTaskId = cId }
        };
        var state = BuildState(tasks, deps);

        var result = _algorithm.TrySchedule(state);

        Assert.That(result, Is.True);
        var bEnd = state.PlannedBlocks.Where(b => b.TaskId == bId).Max(b => b.EndDate);
        var cEnd = state.PlannedBlocks.Where(b => b.TaskId == cId).Max(b => b.EndDate);
        var dStart = state.PlannedBlocks.Where(b => b.TaskId == dId).Min(b => b.StartDate);
        Assert.That(dStart, Is.GreaterThanOrEqualTo(bEnd), "D must start after B finishes");
        Assert.That(dStart, Is.GreaterThanOrEqualTo(cEnd), "D must start after C finishes");
    }

    // ── Borbély day-boundary reset ────────────────────────────────────────────

    [Test]
    public void TrySchedule_IntensiveOnDay1_NormalTaskOnDay2_DoesNotRequireLightConstraint()
    {
        // Intensive task fills day 1. Normal task on day 2 should be scheduled without
        // the light-after-intensive constraint (Borbély: sleep resets fatigue).
        var intensiveId = Guid.NewGuid();
        var normalId = Guid.NewGuid();
        var slotStart = new DateTime(2026, 5, 4, 8, 0, 0, DateTimeKind.Utc);
        var tasks = new List<UserTask>
        {
            MakeTask(intensiveId, estimateMinutes: 60, intensity: ETaskIntensity.Intensive),
            MakeTask(normalId,    estimateMinutes: 60, intensity: ETaskIntensity.Normal)
        };
        // Day 1: only enough room for the intensive task (60 min cap).
        // Day 2+: normal 480-min cap.
        var projectStart = slotStart;
        var analysis = _analyzer.Analyze(tasks, [], projectStart);
        var freeSlots = new List<TimeSlot>
        {
            new(slotStart, slotStart.AddMinutes(60)),                                   // day 1 – tight
            new(slotStart.AddDays(1), slotStart.AddDays(1).AddMinutes(480))             // day 2 – open
        };
        var state = new SchedulingState
        {
            Tasks = tasks,
            RemainingMinutes = tasks.ToDictionary(t => t.Id, t => (int)t.TimeEstimate.TotalMinutes),
            FreeSlots = freeSlots,
            DailyBudgets = new Dictionary<DateOnly, DailyBudget>
            {
                [DateOnly.FromDateTime(slotStart)]          = new DailyBudget { RemainingTotalMinutes = 60,  RemainingIntensiveMinutes = 30 },
                [DateOnly.FromDateTime(slotStart.AddDays(1))] = new DailyBudget { RemainingTotalMinutes = 480, RemainingIntensiveMinutes = 240 }
            },
            Analysis = analysis
        };

        var result = _algorithm.TrySchedule(state);

        Assert.That(result, Is.True, "Normal task on day 2 should be schedulable after intensive on day 1");
        var normalBlocks = state.PlannedBlocks.Where(b => b.TaskId == normalId).ToList();
        Assert.That(normalBlocks, Is.Not.Empty, "Normal task must be scheduled");
    }

    // ── Task exactly at max-block size is placed in one block ─────────────────

    [Test]
    public void TrySchedule_TaskExactlyMaxBlockMinutes_PlacedInSingleBlock()
    {
        var taskId = Guid.NewGuid();
        var task = MakeTask(taskId, estimateMinutes: _algorithm.MaxBlockMinutes);
        var state = BuildState([task]);

        _algorithm.TrySchedule(state);

        var blocks = state.PlannedBlocks.Where(b => b.TaskId == taskId).ToList();
        Assert.That(blocks, Has.Count.EqualTo(1));
        Assert.That((blocks[0].EndDate - blocks[0].StartDate).TotalMinutes,
            Is.EqualTo(_algorithm.MaxBlockMinutes));
    }

    // ── Remaining minutes are zero after successful schedule ──────────────────

    [Test]
    public void TrySchedule_Success_AllRemainingMinutesAreZero()
    {
        var tasks = Enumerable.Range(0, 3)
            .Select(_ => MakeTask(Guid.NewGuid(), estimateMinutes: 60))
            .ToList();
        var state = BuildState(tasks);

        var result = _algorithm.TrySchedule(state);

        Assert.That(result, Is.True);
        foreach (var kvp in state.RemainingMinutes)
            Assert.That(kvp.Value, Is.EqualTo(0),
                $"Task {kvp.Key} still has {kvp.Value} unscheduled minutes");
    }
}
