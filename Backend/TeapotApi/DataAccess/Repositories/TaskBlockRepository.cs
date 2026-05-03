using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class TaskBlockRepository(TeapotDbContext context): ITaskBlockRepository
{
    private readonly DbSet<TaskBlock> _dbSet = context.Set<TaskBlock>();
    public async Task<IEnumerable<TaskBlock>> GetFixedTaskBlocksForTaskIdsAsync(List<Guid> taskIds, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(tb => taskIds.Contains(tb.TaskId) && tb.IsFixed).ToListAsync();
    }
}