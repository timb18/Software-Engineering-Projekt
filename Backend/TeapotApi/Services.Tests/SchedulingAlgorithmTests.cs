using DataAccess.Models;

namespace Services.Tests;

[TestFixture]
public class SchedulingAlgorithmTests
{
    [SetUp]
    public void SetUp()
    {
        _scheduler = new GreedyScheduler();
        _analyzer = new DependencyAnalyzer();
    }

    private GreedyScheduler _scheduler = null!;
    private DependencyAnalyzer _analyzer = null!;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly DateTime T0 = new(2026, 5, 4, 8, 0, 0, DateTimeKind.Utc);

    private static UserTask MakeTask(
        Guid id,
        int estimateMinutes = 60,
        DateTime? deadline = null,
        ETaskPriority priority = ETaskPriority.Medium,
        bool isFixed = false,
        DateTime? createdAt = null)
    {
        return new UserTask
        {
            Id = id,
            Name = $"Task-{id}",
            TimeEstimate = TimeSpan.FromMinutes(estimateMinutes),
            Deadline = deadline,
            Priority = priority,
            Intensity = ETaskIntensity.Normal,
            IsFixed = isFixed,
            CreatedAt = createdAt ?? T0,
            Status = "todo"
        };
    }

    /// <summary>Builds N days of free slots, each <paramref name="minutesPerDay" /> long, starting at T0.</summary>
    private static List<TimeSlot> MakeSlots(int days = 7, int minutesPerDay = 480, DateTime? from = null)
    {
        var start = from ?? T0;
        return Enumerable.Range(0, days)
            .Select(d => new TimeSlot(start.AddDays(d), start.AddDays(d).AddMinutes(minutesPerDay)))
            .ToList();
    }

    private DependencyAnalysisResult Analyze(
        IReadOnlyList<UserTask> tasks,
        IReadOnlyList<TaskDependency>? deps = null)
    {
        return _analyzer.Analyze(tasks, deps ?? [], T0);
    }

    /// <summary>Convenience wrapper: builds remaining-minutes from TimeEstimate and schedules.</summary>
    private List<TaskBlock> Schedule(
        IReadOnlyList<UserTask> tasks,
        List<TimeSlot> slots,
        IReadOnlyList<TaskDependency>? deps = null,
        IReadOnlySet<Guid>? alreadyScheduled = null)
    {
        var remaining = tasks.ToDictionary(t => t.Id, t => (int)t.TimeEstimate.TotalMinutes);
        var analysis = Analyze(tasks, deps);
        return _scheduler.Schedule(tasks, remaining, alreadyScheduled ?? new HashSet<Guid>(), slots, analysis);
    }

    // ── Basic scheduling ──────────────────────────────────────────────────────

    [Test]
    public void Schedule_NoTasks_ReturnsEmptyList()
    {
        var blocks = Schedule([], MakeSlots());

        Assert.That(blocks, Is.Empty);
    }

    [Test]
    public void Schedule_SingleTask_FullyScheduled()
    {
        var id = Guid.NewGuid();
        var task = MakeTask(id);

        var blocks = Schedule([task], MakeSlots());

        var scheduled = blocks.Where(b => b.TaskId == id).Sum(b => (b.EndDate - b.StartDate).TotalMinutes);
        Assert.That(scheduled, Is.EqualTo(60));
    }

    [Test]
    public void Schedule_MultipleTasks_AllFullyScheduled()
    {
        var tasks = Enumerable.Range(0, 4).Select(_ => MakeTask(Guid.NewGuid())).ToList();

        var blocks = Schedule(tasks, MakeSlots());

        foreach (var task in tasks)
        {
            var scheduled = blocks.Where(b => b.TaskId == task.Id).Sum(b => (b.EndDate - b.StartDate).TotalMinutes);
            Assert.That(scheduled, Is.EqualTo(60), $"Task {task.Id} not fully scheduled");
        }
    }

    [Test]
    public void Schedule_LargeTask_SplitsAcrossMultipleSlots()
    {
        // One 10-hour task, 8-hour slots → must spill into day 2
        var id = Guid.NewGuid();
        var task = MakeTask(id, 600);

        var blocks = Schedule([task], MakeSlots());

        var days = blocks.Select(b => DateOnly.FromDateTime(b.StartDate)).Distinct().Count();
        Assert.That(days, Is.GreaterThan(1));
        var total = blocks.Where(b => b.TaskId == id).Sum(b => (b.EndDate - b.StartDate).TotalMinutes);
        Assert.That(total, Is.EqualTo(600));
    }

    // ── Min block size: short slots are skipped ───────────────────────────────

    [Test]
    public void Schedule_SlotsUnder25Min_AreSkipped()
    {
        var id = Guid.NewGuid();
        var task = MakeTask(id);
        // Three tiny slots (10 min each) then one valid slot
        var slots = new List<TimeSlot>
        {
            new(T0, T0.AddMinutes(10)),
            new(T0.AddMinutes(15), T0.AddMinutes(24)),
            new(T0.AddMinutes(30), T0.AddMinutes(39)),
            new(T0.AddHours(1), T0.AddHours(2)) // valid: 60 min
        };
        var remaining = new Dictionary<Guid, int> { [id] = 60 };
        var analysis = Analyze([task]);

        var blocks = _scheduler.Schedule([task], remaining, new HashSet<Guid>(), slots, analysis);

        Assert.That(blocks, Has.Count.EqualTo(1));
        Assert.That(blocks[0].StartDate, Is.EqualTo(T0.AddHours(1)));
    }

    [Test]
    public void Schedule_PlacedBlocks_NeverBelowMinBlockMinutes()
    {
        var tasks = Enumerable.Range(0, 3).Select(_ => MakeTask(Guid.NewGuid())).ToList();

        var blocks = Schedule(tasks, MakeSlots());

        foreach (var block in blocks)
        {
            var dur = (block.EndDate - block.StartDate).TotalMinutes;
            Assert.That(dur, Is.GreaterThanOrEqualTo(_scheduler.MinBlockMinutes),
                $"Block of {dur} min is below minimum");
        }
    }

    // ── No overlapping blocks ─────────────────────────────────────────────────

    [Test]
    public void Schedule_MultipleTasks_BlocksDoNotOverlap()
    {
        var tasks = Enumerable.Range(0, 5).Select(_ => MakeTask(Guid.NewGuid(), 90)).ToList();

        var blocks = Schedule(tasks, MakeSlots())
            .OrderBy(b => b.StartDate)
            .ToList();

        for (var i = 1; i < blocks.Count; i++)
            Assert.That(blocks[i].StartDate, Is.GreaterThanOrEqualTo(blocks[i - 1].EndDate),
                $"Blocks {i - 1} and {i} overlap");
    }

    // ── Dependency ordering ───────────────────────────────────────────────────

    [Test]
    public void Schedule_DependentTasks_PredecessorScheduledFirst()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var tasks = new List<UserTask> { MakeTask(aId), MakeTask(bId) };
        var deps = new List<TaskDependency> { new() { TaskId = bId, DependsOnTaskId = aId } };

        var blocks = Schedule(tasks, MakeSlots(), deps);

        var aEnd = blocks.Where(b => b.TaskId == aId).Max(b => b.EndDate);
        var bStart = blocks.Where(b => b.TaskId == bId).Min(b => b.StartDate);
        Assert.That(bStart, Is.GreaterThanOrEqualTo(aEnd), "B must start after A finishes");
    }

    [Test]
    public void Schedule_DiamondDependency_DScheduledAfterBAndC()
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

        var blocks = Schedule(tasks, MakeSlots(), deps);

        var bEnd = blocks.Where(b => b.TaskId == bId).Max(b => b.EndDate);
        var cEnd = blocks.Where(b => b.TaskId == cId).Max(b => b.EndDate);
        var dStart = blocks.Where(b => b.TaskId == dId).Min(b => b.StartDate);
        Assert.That(dStart, Is.GreaterThanOrEqualTo(bEnd), "D must start after B finishes");
        Assert.That(dStart, Is.GreaterThanOrEqualTo(cEnd), "D must start after C finishes");
    }

    [Test]
    public void Schedule_AlreadyScheduledPredecessor_DependentBecomesReady()
    {
        // A is already done (passed in alreadyScheduledIds). B depends on A and should be scheduled.
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var taskA = MakeTask(aId);
        var taskB = MakeTask(bId);
        var deps = new List<TaskDependency> { new() { TaskId = bId, DependsOnTaskId = aId } };

        var remaining = new Dictionary<Guid, int> { [bId] = 60 }; // only B needs scheduling
        var analysis = Analyze([taskA, taskB], deps);
        var blocks = _scheduler.Schedule(
            [taskB], remaining, new HashSet<Guid> { aId }, MakeSlots(), analysis);

        var scheduled = blocks.Where(b => b.TaskId == bId).Sum(b => (b.EndDate - b.StartDate).TotalMinutes);
        Assert.That(scheduled, Is.EqualTo(60));
    }

    // ── Deadline ordering ─────────────────────────────────────────────────────

    [Test]
    public void Schedule_TaskWithDeadline_AllBlocksBeforeDeadlineMidnight()
    {
        var id = Guid.NewGuid();
        var deadline = T0.Date.AddDays(2); // deadline = Day 3
        var task = MakeTask(id, 60, deadline);

        var blocks = Schedule([task], MakeSlots());

        var bound = deadline.Date.AddDays(1);
        foreach (var block in blocks.Where(b => b.TaskId == id))
            Assert.That(block.EndDate, Is.LessThanOrEqualTo(bound),
                $"Block ends at {block.EndDate}, after deadline bound {bound}");
    }

    [Test]
    public void Schedule_EarlierDeadlineFirst_UrgentTaskScheduledBeforeRelaxedTask()
    {
        // Two independent tasks; the one with the earlier deadline should get the first slot.
        var urgentId = Guid.NewGuid();
        var relaxedId = Guid.NewGuid();
        // createdAt same so only deadline differs
        var urgent = MakeTask(urgentId, 60, T0.AddDays(1));
        var relaxed = MakeTask(relaxedId, 60, T0.AddDays(5));

        var blocks = Schedule([relaxed, urgent], MakeSlots()); // intentionally pass relaxed first

        var urgentStart = blocks.Where(b => b.TaskId == urgentId).Min(b => b.StartDate);
        var relaxedStart = blocks.Where(b => b.TaskId == relaxedId).Min(b => b.StartDate);
        Assert.That(urgentStart, Is.LessThan(relaxedStart),
            "Urgent task (earlier deadline) must be scheduled before relaxed task");
    }

    // ── Priority ordering ─────────────────────────────────────────────────────

    [Test]
    public void Schedule_NoDeadlines_HighPriorityScheduledFirst()
    {
        // Two independent tasks, no deadlines — high priority should get earlier slots.
        var highId = Guid.NewGuid();
        var lowId = Guid.NewGuid();
        var high = MakeTask(highId, priority: ETaskPriority.High);
        var low = MakeTask(lowId, priority: ETaskPriority.Low);

        var blocks = Schedule([low, high], MakeSlots()); // intentionally pass low first

        var highStart = blocks.Where(b => b.TaskId == highId).Min(b => b.StartDate);
        var lowStart = blocks.Where(b => b.TaskId == lowId).Min(b => b.StartDate);
        Assert.That(highStart, Is.LessThan(lowStart),
            "High-priority task must be scheduled before low-priority task");
    }

    // ── No slots / partial scheduling ────────────────────────────────────────

    [Test]
    public void Schedule_NoFreeSlots_ReturnsEmptyBlocks()
    {
        var id = Guid.NewGuid();
        var task = MakeTask(id);
        var remaining = new Dictionary<Guid, int> { [id] = 60 };
        var analysis = Analyze([task]);

        var blocks = _scheduler.Schedule([task], remaining, new HashSet<Guid>(), [], analysis);

        Assert.That(blocks, Is.Empty);
    }

    [Test]
    public void Schedule_NotEnoughSlots_TaskPartiallyScheduled()
    {
        // Only 30 min available, task needs 60 → partial scheduling
        var id = Guid.NewGuid();
        var task = MakeTask(id);
        var slots = new List<TimeSlot> { new(T0, T0.AddMinutes(30)) };
        var remaining = new Dictionary<Guid, int> { [id] = 60 };
        var analysis = Analyze([task]);

        var blocks = _scheduler.Schedule([task], remaining, new HashSet<Guid>(), slots, analysis);

        var scheduled = blocks.Where(b => b.TaskId == id).Sum(b => (b.EndDate - b.StartDate).TotalMinutes);
        Assert.That(scheduled, Is.EqualTo(30));
    }

    // ── Partial past progress ─────────────────────────────────────────────────

    [Test]
    public void Schedule_PartialPastWork_OnlyRemainingMinutesScheduled()
    {
        // Task: 120 min total, 60 min already done → only 60 min should be scheduled
        var id = Guid.NewGuid();
        var task = MakeTask(id, 120);
        var remaining = new Dictionary<Guid, int> { [id] = 60 }; // caller already subtracted past work
        var analysis = Analyze([task]);

        var blocks = _scheduler.Schedule([task], remaining, new HashSet<Guid>(), MakeSlots(), analysis);

        var scheduled = blocks.Where(b => b.TaskId == id).Sum(b => (b.EndDate - b.StartDate).TotalMinutes);
        Assert.That(scheduled, Is.EqualTo(60));
    }

    // ── Blocked by deadline ───────────────────────────────────────────────────

    [Test]
    public void Schedule_DeadlineAlreadyPassed_NoBlocksCreated()
    {
        var id = Guid.NewGuid();
        // Deadline in the past relative to slot times
        var deadline = T0.AddDays(-1);
        var task = MakeTask(id, 60, deadline);
        var remaining = new Dictionary<Guid, int> { [id] = 60 };
        var analysis = _analyzer.Analyze([task], [], deadline.AddDays(-2));

        var blocks = _scheduler.Schedule([task], remaining, new HashSet<Guid>(), MakeSlots(), analysis);

        Assert.That(blocks.Where(b => b.TaskId == id), Is.Empty,
            "Task with past deadline should not be scheduled");
    }

    // ── Deadline as exact timestamp ───────────────────────────────────────────

    [Test]
    public void Schedule_DeadlineWithTimeOfDay_BlocksEndAtOrBeforeDeadline()
    {
        // Slot 08:00–16:00 on T0, deadline at 12:30 same day.
        // Task estimate 4h → should be capped to 4.5h available before deadline.
        var id = Guid.NewGuid();
        var deadline = T0.Date.AddHours(12).AddMinutes(30); // 12:30
        var task = MakeTask(id, 240, deadline);
        var slots = new List<TimeSlot> { new(T0, T0.AddHours(8)) }; // 08:00–16:00

        var remaining = new Dictionary<Guid, int> { [id] = 240 };
        var analysis = _analyzer.Analyze([task], [], T0);
        var blocks = _scheduler.Schedule([task], remaining, new HashSet<Guid>(), slots, analysis);

        Assert.That(blocks, Is.Not.Empty);
        foreach (var b in blocks)
            Assert.That(b.EndDate, Is.LessThanOrEqualTo(deadline),
                $"Block ends at {b.EndDate} which is after deadline {deadline}");
    }

    // ── Intensive break ───────────────────────────────────────────────────────

    [Test]
    public void Schedule_IntensiveTask_LeavesRecoveryGapBeforeNextTask()
    {
        // Intensive task fills part of a slot, a normal task takes the rest.
        // Expect a recovery gap (IntensiveBreakMinutes) between the last intensive block
        // and the next task. The intensive task itself may be split across multiple blocks
        // due to the max-focus cap.
        var intensiveId = Guid.NewGuid();
        var normalId = Guid.NewGuid();
        var intensive = MakeTask(intensiveId);
        intensive.Intensity = ETaskIntensity.Intensive;
        // Same createdAt; sort priority high so intensive is picked first
        intensive.Priority = ETaskPriority.High;
        var normal = MakeTask(normalId);
        normal.Priority = ETaskPriority.Low;

        var slots = new List<TimeSlot> { new(T0, T0.AddHours(8)) };
        var blocks = Schedule([intensive, normal], slots);

        var intensiveBlocks = blocks.Where(b => b.TaskId == intensiveId).ToList();
        var normalBlock = blocks.Single(b => b.TaskId == normalId);
        Assert.That(intensiveBlocks, Is.Not.Empty);
        var intensiveEnd = intensiveBlocks.Max(b => b.EndDate);
        Assert.That(normalBlock.StartDate,
            Is.GreaterThanOrEqualTo(intensiveEnd.AddMinutes(_scheduler.IntensiveBreakMinutes)),
            $"Normal task must start at least {_scheduler.IntensiveBreakMinutes} min after intensive task ends");
    }

    // ── Science-based focus / break pacing ────────────────────────────────────

    [Test]
    public void Schedule_LongNormalTask_SplitsInto90MinBlocksWith15MinBreaks()
    {
        // Normal task of 4 hours in a single long slot → expect blocks capped at
        // NormalMaxFocusMinutes (90) with NormalBreakMinutes (15) between them.
        var id = Guid.NewGuid();
        var task = MakeTask(id, 240);
        var slots = new List<TimeSlot> { new(T0, T0.AddHours(10)) };

        var blocks = Schedule([task], slots).OrderBy(b => b.StartDate).ToList();

        Assert.That(blocks, Has.Count.GreaterThanOrEqualTo(2),
            "Long normal task must be split into multiple blocks");
        foreach (var b in blocks)
        {
            var dur = (int)(b.EndDate - b.StartDate).TotalMinutes;
            Assert.That(dur, Is.LessThanOrEqualTo(_scheduler.NormalMaxFocusMinutes),
                $"Block of {dur} min exceeds normal max focus {_scheduler.NormalMaxFocusMinutes}");
        }

        for (var i = 1; i < blocks.Count; i++)
        {
            var gap = (int)(blocks[i].StartDate - blocks[i - 1].EndDate).TotalMinutes;
            Assert.That(gap, Is.GreaterThanOrEqualTo(_scheduler.NormalBreakMinutes),
                $"Gap between block {i - 1} and {i} is {gap} min, expected >= {_scheduler.NormalBreakMinutes}");
        }

        var total = blocks.Sum(b => (int)(b.EndDate - b.StartDate).TotalMinutes);
        Assert.That(total, Is.EqualTo(240), "Total scheduled time must still equal task estimate");
    }

    [Test]
    public void Schedule_LongIntensiveTask_SplitsInto50MinBlocksWith15MinBreaks()
    {
        var id = Guid.NewGuid();
        var task = MakeTask(id, 120);
        task.Intensity = ETaskIntensity.Intensive;
        var slots = new List<TimeSlot> { new(T0, T0.AddHours(8)) };

        var blocks = Schedule([task], slots).OrderBy(b => b.StartDate).ToList();

        Assert.That(blocks, Has.Count.GreaterThanOrEqualTo(2));
        foreach (var b in blocks)
        {
            var dur = (int)(b.EndDate - b.StartDate).TotalMinutes;
            Assert.That(dur, Is.LessThanOrEqualTo(_scheduler.IntensiveMaxFocusMinutes),
                $"Intensive block of {dur} min exceeds max focus {_scheduler.IntensiveMaxFocusMinutes}");
        }

        for (var i = 1; i < blocks.Count; i++)
        {
            var gap = (int)(blocks[i].StartDate - blocks[i - 1].EndDate).TotalMinutes;
            Assert.That(gap, Is.GreaterThanOrEqualTo(_scheduler.IntensiveBreakMinutes));
        }
    }

    [Test]
    public void Schedule_LongLightTask_NotSplit_NoBreaks()
    {
        // Light tasks are low cognitive load and need no enforced breaks → no cap, no break.
        var id = Guid.NewGuid();
        var task = MakeTask(id, 300);
        task.Intensity = ETaskIntensity.Light;
        var slots = new List<TimeSlot> { new(T0, T0.AddHours(10)) };

        var blocks = Schedule([task], slots);

        Assert.That(blocks, Has.Count.EqualTo(1), "Light task must not be split");
        var dur = (int)(blocks[0].EndDate - blocks[0].StartDate).TotalMinutes;
        Assert.That(dur, Is.EqualTo(300));
    }

    [Test]
    public void Schedule_TwoNormalTasksInSameSlot_HaveBreakBetweenThem()
    {
        // Two normal tasks back-to-back must have a recovery gap (context switch).
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var a = MakeTask(aId);
        a.Priority = ETaskPriority.High;
        var b = MakeTask(bId);
        b.Priority = ETaskPriority.Low;
        var slots = new List<TimeSlot> { new(T0, T0.AddHours(4)) };

        var blocks = Schedule([a, b], slots).OrderBy(x => x.StartDate).ToList();

        Assert.That(blocks, Has.Count.EqualTo(2));
        var gap = (int)(blocks[1].StartDate - blocks[0].EndDate).TotalMinutes;
        Assert.That(gap, Is.GreaterThanOrEqualTo(_scheduler.NormalBreakMinutes),
            $"Gap between two normal tasks is {gap} min, expected >= {_scheduler.NormalBreakMinutes}");
    }

    [Test]
    public void Schedule_TwoLightTasksInSameSlot_NoBreakBetweenThem()
    {
        // Two light tasks need no enforced break.
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var a = MakeTask(aId);
        a.Intensity = ETaskIntensity.Light;
        a.Priority = ETaskPriority.High;
        var b = MakeTask(bId);
        b.Intensity = ETaskIntensity.Light;
        b.Priority = ETaskPriority.Low;
        var slots = new List<TimeSlot> { new(T0, T0.AddHours(4)) };

        var blocks = Schedule([a, b], slots).OrderBy(x => x.StartDate).ToList();

        Assert.That(blocks, Has.Count.EqualTo(2));
        var gap = (int)(blocks[1].StartDate - blocks[0].EndDate).TotalMinutes;
        Assert.That(gap, Is.EqualTo(0));
    }
}