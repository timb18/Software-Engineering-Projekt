using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class OrganizationRepository(TeapotDbContext context) : IOrganizationRepository
{
    public Task<Organization?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Organizations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Organization?> FindByNameAsync(string name, CancellationToken cancellationToken = default) =>
        context.Organizations.FirstOrDefaultAsync(o => o.Name == name, cancellationToken);

    public async Task<IEnumerable<Organization>> GetForUserAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        await context.Organizations
            .Include(o => o.Memberships).ThenInclude(m => m.User)
            .Include(o => o.Invitations)
            .Where(o => o.Memberships.Any(m => m.User.Email.ToLower() == normalizedEmail))
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

    public Task<Organization?> GetWithMembershipsAndInvitationsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Organizations
            .Include(o => o.Memberships).ThenInclude(m => m.WorkProfile)
            .Include(o => o.Invitations)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        context.Organizations.Add(organization);
        await context.SaveChangesAsync(cancellationToken);
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

                var userTasks = await context.UserTasks
                    .Where(t => t.WorkProfileId == workProfileId)
                    .ToListAsync(cancellationToken);
                if (userTasks.Count > 0) context.UserTasks.RemoveRange(userTasks);

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
