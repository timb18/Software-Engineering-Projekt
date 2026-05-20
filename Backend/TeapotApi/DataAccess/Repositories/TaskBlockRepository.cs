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
        // without any blocks (delete succeeded but inserts failed). When the caller already
        // started an outer transaction (e.g. the planner) we simply join it instead of nesting.
        var hasOuterTransaction = context.Database.CurrentTransaction is not null;
        await using var tx = hasOuterTransaction
            ? null
            : await context.Database.BeginTransactionAsync(cancellationToken);

        // Delete only FUTURE non-fixed blocks. Past blocks are historical facts (used to
        // compute already-completed work for partial-progress accounting) and must be kept.
        // Fixed blocks are user-pinned and never overwritten by the scheduler.
        var now = DateTime.UtcNow;
        await context.TaskBlocks
            .Where(b => taskIds.Contains(b.TaskId) && !b.IsFixed && b.StartDate >= now)
            .ExecuteDeleteAsync(cancellationToken);

        await context.TaskBlocks.AddRangeAsync(newBlocks, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        if (tx is not null)
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
        try
        {
            var existing = await context.TaskBlocks
                .Where(b => b.TaskId == taskId).FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                context.TaskBlocks.Update(existing);
            }
            else
            {
                var newTaskBlock = new TaskBlock()
                {
                    TaskId = taskId,
                    StartDate = start,
                    EndDate = end,
                    IsFixed = true
                };
                await context.TaskBlocks.AddAsync(newTaskBlock, cancellationToken);
            }
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            throw e.InnerException;
        }
        
    }
}
