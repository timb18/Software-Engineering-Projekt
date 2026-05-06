using DataAccess.Models;

namespace DataAccess.Repositories;

public interface ITaskBlockRepository
{
    Task<IReadOnlyList<TaskBlock>> GetByWorkProfileAsync(
        Guid workProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all non-fixed task blocks for the given work profile and replaces
    /// them with the provided blocks.
    /// </summary>
    Task ReplaceAsync(
        Guid workProfileId,
        IReadOnlyList<TaskBlock> newBlocks,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes all blocks for a specific task.</summary>
    Task DeleteForTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes any existing blocks for the task and inserts a single fixed block
    /// covering [start, end]. Used when a task has IsFixed=true.
    /// </summary>
    Task UpsertFixedBlockAsync(Guid taskId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}
