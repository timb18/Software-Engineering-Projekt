using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class TaskBlockRepository(TeapotDbContext context) : ITaskBlockRepository
{
    public async Task<IReadOnlyList<TaskBlock>> GetByWorkProfileAsync(
        Guid workProfileId, CancellationToken cancellationToken = default)
    {
        var taskIds = await context.UserTasks
            .Where(t => t.WorkProfileId == workProfileId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        return await context.TaskBlocks
            .Where(b => taskIds.Contains(b.TaskId))
            .Include(b => b.Task)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceAsync(
        Guid workProfileId,
        IReadOnlyList<TaskBlock> newBlocks,
        CancellationToken cancellationToken = default)
    {
        var taskIds = await context.UserTasks
            .Where(t => t.WorkProfileId == workProfileId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        // Wrap delete + inserts in a transaction so a partial failure never leaves the profile
        // without any blocks (delete succeeded but inserts failed).
        await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);

        // Delete all non-fixed blocks for these tasks
        await context.TaskBlocks
            .Where(b => taskIds.Contains(b.TaskId) && !b.IsFixed)
            .ExecuteDeleteAsync(cancellationToken);

        // Insert new blocks via raw SQL (TaskBlock is a keyless entity)
        foreach (var block in newBlocks)
        {
            await context.Database.ExecuteSqlAsync(
                $"INSERT INTO task_blocks (task_id, start_date, end_date, is_fixed) VALUES ({block.TaskId}, {block.StartDate}, {block.EndDate}, {block.IsFixed})",
                cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    public async Task DeleteForTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var blocks = await context.TaskBlocks
            .Where(b => b.TaskId == taskId)
            .ToListAsync(cancellationToken);
        context.TaskBlocks.RemoveRange(blocks);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertFixedBlockAsync(
        Guid taskId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        var existing = await context.TaskBlocks
            .Where(b => b.TaskId == taskId)
            .ToListAsync(cancellationToken);
        context.TaskBlocks.RemoveRange(existing);
        await context.SaveChangesAsync(cancellationToken);

        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO task_blocks (task_id, start_date, end_date, is_fixed) VALUES ({taskId}, {start}, {end}, true)",
            cancellationToken);
    }
}
