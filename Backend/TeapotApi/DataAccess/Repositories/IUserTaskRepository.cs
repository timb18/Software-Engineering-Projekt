using DataAccess.Models;

namespace DataAccess.Repositories;

/// <summary>
/// Data access interface for UserTask entity operations.
/// Manages tasks assigned to users and their properties.
/// </summary>
/// <remarks>
/// UserTasks represent work items that need to be scheduled and completed.
/// Tasks can have dependencies on other tasks, deadlines, and fixed schedule blocks.
/// </remarks>
public interface IUserTaskRepository
{
    /// <summary>
    /// Gets all tasks assigned to a user within a work profile.
    /// </summary>
    /// <param name="workProfileId">The work profile GUID</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>List of all tasks for this work profile</returns>
    /// <remarks>Used by the scheduling algorithm and task list endpoints</remarks>
    Task<IEnumerable<UserTask>> GetByWorkProfileAsync(Guid workProfileId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Finds a specific task within a work profile.
    /// </summary>
    /// <param name="taskId">The task GUID</param>
    /// <param name="workProfileId">The work profile GUID (for validation)</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>The task if found and belongs to the work profile, null otherwise</returns>
    Task<UserTask?> FindAsync(Guid taskId, Guid workProfileId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new task and persists it to the database.
    /// </summary>
    /// <param name="task">The new task entity</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    Task AddAsync(UserTask task, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates an existing task.
    /// </summary>
    /// <param name="task">The task with updated values</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <remarks>Persists all property changes (name, description, priority, estimates, etc.)</remarks>
    Task UpdateAsync(UserTask task, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a task from the database.
    /// </summary>
    /// <param name="task">The task to delete</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <remarks>Also deletes associated task blocks and dependencies</remarks>
    Task DeleteAsync(UserTask task, CancellationToken cancellationToken = default);
}
