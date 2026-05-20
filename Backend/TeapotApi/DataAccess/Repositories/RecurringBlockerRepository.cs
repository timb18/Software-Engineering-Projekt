using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class RecurringBlockerRepository(TeapotDbContext context) : IRecurringBlockerRepository
{
    public async Task<IReadOnlyList<RecurringBlocker>> GetByWorkProfileAsync(
        Guid workProfileId, CancellationToken cancellationToken = default)
    {
        return await context.RecurringBlockers
            .Where(b => b.WorkProfileId == workProfileId)
            .ToListAsync(cancellationToken);
    }

    public Task<RecurringBlocker?> GetByIdAsync(
        Guid workProfileId, Guid blockerId, CancellationToken cancellationToken = default)
    {
        return context.RecurringBlockers
            .FirstOrDefaultAsync(b => b.WorkProfileId == workProfileId && b.Id == blockerId, cancellationToken);
    }

    public async Task AddAsync(RecurringBlocker blocker, CancellationToken cancellationToken = default)
    {
        context.RecurringBlockers.Add(blocker);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RecurringBlocker blocker, CancellationToken cancellationToken = default)
    {
        context.RecurringBlockers.Update(blocker);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(RecurringBlocker blocker, CancellationToken cancellationToken = default)
    {
        context.RecurringBlockers.Remove(blocker);
        await context.SaveChangesAsync(cancellationToken);
    }
}