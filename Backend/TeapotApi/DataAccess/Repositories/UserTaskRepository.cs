using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class UserTaskRepository(TeapotDbContext context) : IUserTaskRepository
{
    public async Task<IEnumerable<UserTask>> GetByWorkProfileAsync(Guid workProfileId, CancellationToken cancellationToken = default) =>
        await context.UserTasks
            .Where(t => t.WorkProfileId == workProfileId)
            .ToListAsync(cancellationToken);

    public Task<UserTask?> FindAsync(Guid taskId, Guid workProfileId, CancellationToken cancellationToken = default) =>
        context.UserTasks
            .FirstOrDefaultAsync(t => t.Id == taskId && t.WorkProfileId == workProfileId, cancellationToken);

    public async Task AddAsync(UserTask task, CancellationToken cancellationToken = default)
    {
        context.UserTasks.Add(task);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserTask task, CancellationToken cancellationToken = default)
    {
        context.UserTasks.Update(task);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(UserTask task, CancellationToken cancellationToken = default)
    {
        context.UserTasks.Remove(task);
        await context.SaveChangesAsync(cancellationToken);
    }
}
