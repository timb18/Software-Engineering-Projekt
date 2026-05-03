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
}
