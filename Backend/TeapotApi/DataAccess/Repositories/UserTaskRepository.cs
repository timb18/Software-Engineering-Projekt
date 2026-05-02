using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class UserTaskRepository(TeapotDbContext context): GenericRepository<UserTask>(context), IUserTaskRepository
{
    private readonly DbSet<UserTask> _dbSet = context.Set<UserTask>();
    
    public async Task<IEnumerable<UserTask>> GetTasksForWorkProfileIdAsync(Guid workProfileId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(t => t.WorkProfileId == workProfileId).ToListAsync(cancellationToken);
    }
}