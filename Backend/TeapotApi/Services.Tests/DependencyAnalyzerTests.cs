using DataAccess.Models;
using Services.Planning;

namespace Services.Tests;

[TestFixture]
public class DependencyAnalyzerTests
{
    private DependencyAnalyzer _analyzer = null!;
    private DateTime _projectStart;

    [SetUp]
    public void SetUp()
    {
        _analyzer = new DependencyAnalyzer();
        _projectStart = new DateTime(2026, 5, 3, 8, 0, 0, DateTimeKind.Utc);
    }

    private static UserTask MakeTask(Guid id, int estimateMinutes = 60, DateTime? deadline = null,
        ETaskIntensity intensity = ETaskIntensity.Normal, ETaskPriority priority = ETaskPriority.Medium)
        => new()
        {
            Id = id,
            Name = $"Task-{id}",
            TimeEstimate = TimeSpan.FromMinutes(estimateMinutes),
            Deadline = deadline,
            Intensity = intensity,
            Priority = priority,
            CreatedAt = DateTime.UtcNow,
            Status = "todo"
        };

    // ── Empty input ───────────────────────────────────────────────────────────

    [Test]
    public void Analyze_EmptyTaskList_ReturnsEmptyResult()
    {
        var result = _analyzer.Analyze([], [], _projectStart);

        Assert.That(result.TopologicalOrder, Is.Empty);
        Assert.That(result.CriticalTaskIds, Is.Empty);
        Assert.That(result.Predecessors, Is.Empty);
    }

    // ── Single task ───────────────────────────────────────────────────────────

    [Test]
    public void Analyze_SingleTask_IsInTopologicalOrder()
    {
        var id = Guid.NewGuid();
        var task = MakeTask(id);

        var result = _analyzer.Analyze([task], [], _projectStart);

        Assert.That(result.TopologicalOrder, Is.EqualTo(new[] { id }));
    }

    [Test]
    public void Analyze_SingleTask_IsMarkedCritical()
    {
        var id = Guid.NewGuid();
        var task = MakeTask(id);

        var result = _analyzer.Analyze([task], [], _projectStart);

        Assert.That(result.CriticalTaskIds, Contains.Item(id));
    }

    // ── Linear chain A → B ────────────────────────────────────────────────────

    [Test]
    public void Analyze_LinearChain_TopologicalOrderRespectsDependency()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var tasks = new List<UserTask> { MakeTask(aId), MakeTask(bId) };
        var deps = new List<TaskDependency>
        {
            new() { TaskId = bId, DependsOnTaskId = aId }
        };

        var result = _analyzer.Analyze(tasks, deps, _projectStart);

        var aPos = result.TopologicalOrder.ToList().IndexOf(aId);
        var bPos = result.TopologicalOrder.ToList().IndexOf(bId);
        Assert.That(aPos, Is.LessThan(bPos));
    }

    [Test]
    public void Analyze_LinearChain_PredecessorsCorrect()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var tasks = new List<UserTask> { MakeTask(aId), MakeTask(bId) };
        var deps = new List<TaskDependency>
        {
            new() { TaskId = bId, DependsOnTaskId = aId }
        };

        var result = _analyzer.Analyze(tasks, deps, _projectStart);

        Assert.That(result.Predecessors[aId], Is.Empty);
        Assert.That(result.Predecessors[bId], Is.EqualTo(new[] { aId }));
    }

    // ── Cycle detection ───────────────────────────────────────────────────────

    [Test]
    public void Analyze_CyclicDependency_ThrowsInvalidOperationException()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var tasks = new List<UserTask> { MakeTask(aId), MakeTask(bId) };
        var deps = new List<TaskDependency>
        {
            new() { TaskId = bId, DependsOnTaskId = aId },
            new() { TaskId = aId, DependsOnTaskId = bId }
        };

        Assert.Throws<InvalidOperationException>(() => _analyzer.Analyze(tasks, deps, _projectStart));
    }

    [Test]
    public void Analyze_SelfDependency_ThrowsInvalidOperationException()
    {
        var aId = Guid.NewGuid();
        var tasks = new List<UserTask> { MakeTask(aId) };
        var deps = new List<TaskDependency>
        {
            new() { TaskId = aId, DependsOnTaskId = aId }
        };

        Assert.Throws<InvalidOperationException>(() => _analyzer.Analyze(tasks, deps, _projectStart));
    }

    // ── Critical path ─────────────────────────────────────────────────────────

    [Test]
    public void Analyze_ParallelTasks_OnlyLongerPathIsCritical()
    {
        // A (120 min) and B (30 min) are independent – A takes longer → A is critical
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var tasks = new List<UserTask>
        {
            MakeTask(aId, estimateMinutes: 120),
            MakeTask(bId, estimateMinutes: 30)
        };

        var result = _analyzer.Analyze(tasks, [], _projectStart);

        Assert.That(result.CriticalTaskIds, Contains.Item(aId));
        Assert.That(result.CriticalTaskIds, Does.Not.Contain(bId));
    }

    // ── Deadline feasibility ──────────────────────────────────────────────────

    [Test]
    public void Analyze_ImpossibleDeadlineDueToChain_ThrowsInvalidOperationException()
    {
        // A takes 24 h → B can earliest start 24 h from now
        // Deadline for B is only 1 h from now → impossible
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var deadline = _projectStart.AddHours(1);
        var tasks = new List<UserTask>
        {
            MakeTask(aId, estimateMinutes: 24 * 60),
            MakeTask(bId, estimateMinutes: 60, deadline: deadline)
        };
        var deps = new List<TaskDependency>
        {
            new() { TaskId = bId, DependsOnTaskId = aId }
        };

        Assert.Throws<InvalidOperationException>(() => _analyzer.Analyze(tasks, deps, _projectStart));
    }

    [Test]
    public void Analyze_DeadlineAchievable_DoesNotThrow()
    {
        var taskId = Guid.NewGuid();
        var deadline = _projectStart.AddDays(3);
        var task = MakeTask(taskId, estimateMinutes: 60, deadline: deadline);

        Assert.DoesNotThrow(() => _analyzer.Analyze([task], [], _projectStart));
    }

    // ── Dependencies outside the planning set are ignored ─────────────────────

    [Test]
    public void Analyze_DependencyToExternalTask_IsIgnored()
    {
        var aId = Guid.NewGuid();
        var externalId = Guid.NewGuid();
        var tasks = new List<UserTask> { MakeTask(aId) };
        // Dependency references an id not in the task list
        var deps = new List<TaskDependency>
        {
            new() { TaskId = aId, DependsOnTaskId = externalId }
        };

        Assert.DoesNotThrow(() => _analyzer.Analyze(tasks, deps, _projectStart));
    }

    // ── Three-task linear chain ────────────────────────────────────────────────

    [Test]
    public void Analyze_ThreeTaskChain_AllInCorrectTopologicalOrder()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        var tasks = new List<UserTask> { MakeTask(aId), MakeTask(bId), MakeTask(cId) };
        var deps = new List<TaskDependency>
        {
            new() { TaskId = bId, DependsOnTaskId = aId },
            new() { TaskId = cId, DependsOnTaskId = bId }
        };

        var result = _analyzer.Analyze(tasks, deps, _projectStart);

        var order = result.TopologicalOrder.ToList();
        Assert.That(order.IndexOf(aId), Is.LessThan(order.IndexOf(bId)));
        Assert.That(order.IndexOf(bId), Is.LessThan(order.IndexOf(cId)));
    }

    [Test]
    public void Analyze_ThreeTaskChain_AllThreeAreOnCriticalPath()
    {
        // A→B→C: every task is critical (no slack anywhere)
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        var tasks = new List<UserTask> { MakeTask(aId, 60), MakeTask(bId, 60), MakeTask(cId, 60) };
        var deps = new List<TaskDependency>
        {
            new() { TaskId = bId, DependsOnTaskId = aId },
            new() { TaskId = cId, DependsOnTaskId = bId }
        };

        var result = _analyzer.Analyze(tasks, deps, _projectStart);

        Assert.That(result.CriticalTaskIds, Contains.Item(aId));
        Assert.That(result.CriticalTaskIds, Contains.Item(bId));
        Assert.That(result.CriticalTaskIds, Contains.Item(cId));
    }

    // ── Diamond dependency (A→B, A→C, B→D, C→D) ──────────────────────────────

    [Test]
    public void Analyze_DiamondDependency_TopologicalOrderValid()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        var dId = Guid.NewGuid();
        var tasks = new List<UserTask>
        {
            MakeTask(aId, 60), MakeTask(bId, 60), MakeTask(cId, 60), MakeTask(dId, 60)
        };
        var deps = new List<TaskDependency>
        {
            new() { TaskId = bId, DependsOnTaskId = aId },
            new() { TaskId = cId, DependsOnTaskId = aId },
            new() { TaskId = dId, DependsOnTaskId = bId },
            new() { TaskId = dId, DependsOnTaskId = cId }
        };

        var result = _analyzer.Analyze(tasks, deps, _projectStart);

        var order = result.TopologicalOrder.ToList();
        Assert.That(order.IndexOf(aId), Is.LessThan(order.IndexOf(dId)));
        Assert.That(order.IndexOf(bId), Is.LessThan(order.IndexOf(dId)));
        Assert.That(order.IndexOf(cId), Is.LessThan(order.IndexOf(dId)));
    }

    [Test]
    public void Analyze_DiamondDependency_LongerBranchIsCritical()
    {
        // A→B (120 min)→D, A→C (30 min)→D
        // Critical path: A, B, D  (total 120+60+60=240 vs 30+60+60=150)
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        var dId = Guid.NewGuid();
        var tasks = new List<UserTask>
        {
            MakeTask(aId, 60),
            MakeTask(bId, 120),
            MakeTask(cId, 30),
            MakeTask(dId, 60)
        };
        var deps = new List<TaskDependency>
        {
            new() { TaskId = bId, DependsOnTaskId = aId },
            new() { TaskId = cId, DependsOnTaskId = aId },
            new() { TaskId = dId, DependsOnTaskId = bId },
            new() { TaskId = dId, DependsOnTaskId = cId }
        };

        var result = _analyzer.Analyze(tasks, deps, _projectStart);

        Assert.That(result.CriticalTaskIds, Contains.Item(bId), "B is on critical path");
        Assert.That(result.CriticalTaskIds, Does.Not.Contain(cId), "C has slack, not critical");
    }

    // ── Multiple independent tasks ─────────────────────────────────────────────

    [Test]
    public void Analyze_MultipleIndependentTasks_AllPresentInTopologicalOrder()
    {
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var tasks = ids.Select(id => MakeTask(id)).ToList();

        var result = _analyzer.Analyze(tasks, [], _projectStart);

        Assert.That(result.TopologicalOrder, Is.EquivalentTo(ids));
    }

    [Test]
    public void Analyze_MultipleIndependentTasks_NoPredecessors()
    {
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        var tasks = ids.Select(id => MakeTask(id)).ToList();

        var result = _analyzer.Analyze(tasks, [], _projectStart);

        foreach (var id in ids)
            Assert.That(result.Predecessors[id], Is.Empty);
    }
}
