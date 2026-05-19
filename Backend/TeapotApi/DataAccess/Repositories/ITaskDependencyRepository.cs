using DataAccess.Models;

namespace DataAccess.Repositories;

/// <summary>
/// Data access interface for TaskDependency entity operations.
/// Manages precedence relationships between tasks.
/// </summary>
/// <remarks>
/// TaskDependencies define which tasks must be completed before others can start.
/// Used by the scheduling algorithm for critical path analysis and task sequencing.
/// </remarks>
public interface ITaskDependencyRepository
{
    /// <summary>
    /// Gets all task dependencies within a work profile.
    /// </summary>
    /// <param name="workProfileId">The work profile GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>List of all dependency relationships for tasks in this work profile</returns>
    /// <remarks>Used by the scheduling algorithm to understand task dependencies</remarks>
    Task<IReadOnlyList<TaskDependency>> GetByWorkProfileAsync(
        Guid workProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces all dependencies for a specific task with a new set of dependencies.
    /// </summary>
    /// <param name="taskId">The task GUID</param>
    /// <param name="dependsOnIds">List of task IDs that this task depends on (prerequisites)</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <remarks>
    /// First deletes all existing dependencies for this task,
    /// then creates new dependencies for each ID in the list.
    /// Used when a user updates task dependencies.
    /// </remarks>
    Task ReplaceForTaskAsync(
        Guid taskId, IEnumerable<Guid> dependsOnIds, CancellationToken cancellationToken = default);
}
