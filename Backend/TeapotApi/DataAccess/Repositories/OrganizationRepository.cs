using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class OrganizationRepository(TeapotDbContext context) : IOrganizationRepository
{
    public async Task<Organization?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Organizations.FindAsync([id], cancellationToken);
    }

    public Task<Organization?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return context.Organizations.FirstOrDefaultAsync(o => o.Name == name, cancellationToken);
    }

    public async Task<IEnumerable<Organization>> GetForUserAsync(string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        return await context.Organizations
            .Include(o => o.Memberships).ThenInclude(m => m.User)
            .Include(o => o.Memberships).ThenInclude(m => m.WorkProfile)
            .Include(o => o.Invitations)
            .Where(o => o.Memberships.Any(m => m.User.Email == normalizedEmail))
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Organization?> GetWithMembershipsAndInvitationsAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        return context.Organizations
            .Include(o => o.Memberships).ThenInclude(m => m.WorkProfile)
            .Include(o => o.Invitations)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        context.Organizations.Add(organization);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteWithCascadeAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        foreach (var membership in organization.Memberships.ToList())
        {
            if (membership.WorkProfile is not null)
            {
                var workProfileId = membership.WorkProfile.Id;

                var workDayProfiles = await context.WorkDayProfiles
                    .Where(d => d.WorkProfileId == workProfileId)
                    .ToListAsync(cancellationToken);

                if (workDayProfiles.Count > 0)
                {
                    var dayIds = workDayProfiles.Select(d => d.Id).ToList();

                    var workBlocks = await context.WorkBlocks
                        .Where(b => dayIds.Contains(b.WorkDayProfileId))
                        .ToListAsync(cancellationToken);
                    var workBreaks = await context.WorkBreaks
                        .Where(b => dayIds.Contains(b.WorkDayProfileId))
                        .ToListAsync(cancellationToken);

                    if (workBlocks.Count > 0) context.WorkBlocks.RemoveRange(workBlocks);
                    if (workBreaks.Count > 0) context.WorkBreaks.RemoveRange(workBreaks);
                    context.WorkDayProfiles.RemoveRange(workDayProfiles);
                }

                var timeIntervals = await context.WorkProfileTimeIntervals
                    .Where(i => i.WorkProfileId == workProfileId)
                    .ToListAsync(cancellationToken);
                if (timeIntervals.Count > 0) context.WorkProfileTimeIntervals.RemoveRange(timeIntervals);

                var recurringBlockers = await context.RecurringBlockers
                    .Where(b => b.WorkProfileId == workProfileId)
                    .ToListAsync(cancellationToken);
                if (recurringBlockers.Count > 0) context.RecurringBlockers.RemoveRange(recurringBlockers);

                var userTasks = await context.UserTasks
                    .Where(t => t.WorkProfileId == workProfileId)
                    .ToListAsync(cancellationToken);
                if (userTasks.Count > 0)
                {
                    var taskIds = userTasks.Select(t => t.Id).ToList();

                    var taskBlocks = await context.TaskBlocks
                        .Where(b => taskIds.Contains(b.TaskId))
                        .ToListAsync(cancellationToken);
                    if (taskBlocks.Count > 0) context.TaskBlocks.RemoveRange(taskBlocks);

                    var taskDependencies = await context.TaskDependencies
                        .Where(d => taskIds.Contains(d.TaskId) || taskIds.Contains(d.DependsOnTaskId))
                        .ToListAsync(cancellationToken);
                    if (taskDependencies.Count > 0) context.TaskDependencies.RemoveRange(taskDependencies);

                    context.UserTasks.RemoveRange(userTasks);
                }

                context.WorkProfiles.Remove(membership.WorkProfile);
            }

            context.Memberships.Remove(membership);
        }

        if (organization.Invitations.Count > 0)
            context.Invitations.RemoveRange(organization.Invitations);

        context.Organizations.Remove(organization);
        await context.SaveChangesAsync(cancellationToken);
    }
}
