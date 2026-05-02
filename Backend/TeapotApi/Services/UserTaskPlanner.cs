using DataAccess.Models;
using DataAccess.Repositories;

namespace Services;

public class UserTaskPlanner : IUserTaskPlanner
{
    private readonly SchedulingAlgorithm _algorithm;
    private readonly IUserTaskRepository _userTaskRepository;
    private readonly ITaskDependencyRepository _taskDependencyRepository;
    private readonly IWorkProfileRepository _workProfileRepository;
    private readonly ITaskBlockRepository _taskBlockRepository;

    public UserTaskPlanner(SchedulingAlgorithm algorithm,
        IUserTaskRepository userTaskRepository,
        ITaskDependencyRepository taskDependencyRepository,
        IWorkProfileRepository workProfileRepository,
        ITaskBlockRepository taskBlockRepository
        )
    {
        _algorithm = algorithm;
        _userTaskRepository = userTaskRepository;
        _taskDependencyRepository = taskDependencyRepository;
        _workProfileRepository = workProfileRepository;
        _taskBlockRepository = taskBlockRepository;
    }

    public async Task<(List<TaskBlock> NewBlocks, List<string> Conflicts, List<string> Warnings)> PlanTasks(Guid workProfileId, CancellationToken cancellationToken = default)
    {
        var userTasks = await _userTaskRepository.GetTasksForWorkProfileIdAsync(workProfileId, cancellationToken);
        var taskDependencies = await _taskDependencyRepository.GetTaskDependeciesForUserTasks(userTasks.ToList(), cancellationToken);
        var workProfile = await _workProfileRepository.GetWorkProfileWithWorkDayProfileByIdAsync(workProfileId, cancellationToken);
        var fixedTaskBlocks = new  List<TaskBlock>();
        foreach (var task in userTasks)
        {
            if (task.IsFixed)
            {
                var taskBlock = new TaskBlock()
                {
                    TaskId = task.Id,
                    StartDate = task.EarlyStart,
                    EndDate = task.LateFinish,
                    Task = task,
                    IsFixed = task.IsFixed,
                };
                fixedTaskBlocks.Add(taskBlock);
            }
        }
        return _algorithm.PlanTasks(userTasks.ToList(), taskDependencies.ToList(), workProfile, fixedTaskBlocks.ToList(), DateTime.Now, DateTime.Now.AddDays(7));
    }
}