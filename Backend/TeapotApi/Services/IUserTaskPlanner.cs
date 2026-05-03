using DataAccess.Models;

namespace Services;

public interface IUserTaskPlanner
{
    Task<(List<TaskBlock> NewBlocks, List<string> Conflicts, List<string> Warnings)> PlanTasks(Guid workProfileId,
        CancellationToken cancellationToken = default);
}