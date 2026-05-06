using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class MembershipRepository(TeapotDbContext context) : IMembershipRepository
{
    private const string PersonalWorkspaceDescription = "Auto-created personal workspace";

    public Task<Membership?> FindAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default) =>
        context.Memberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == organizationId, cancellationToken);

    public Task<Membership?> FindWithWorkProfileAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default) =>
        context.Memberships
            .Include(m => m.WorkProfile)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == organizationId, cancellationToken);

    public Task<Membership?> FindOrganizerAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
        context.Memberships
            .FirstOrDefaultAsync(m =>
                m.OrganizationId == organizationId &&
                m.UserId == userId &&
                m.Role == ERole.Organizer, cancellationToken);

    public Task<bool> IsMemberByEmailAsync(Guid organizationId, string normalizedEmail, CancellationToken cancellationToken = default) =>
        context.Memberships
            .AnyAsync(m => m.OrganizationId == organizationId && m.User.Email == normalizedEmail, cancellationToken);

    public Task<Membership?> FindPersonalAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.Memberships
            .Include(m => m.Organization)
            .Where(m => m.UserId == userId)
            .OrderByDescending(m =>
                m.Role == ERole.Organizer &&
                m.Organization.MaxUsers == 1 &&
                m.Organization.Description == PersonalWorkspaceDescription)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(Membership membership, CancellationToken cancellationToken = default)
    {
        context.Memberships.Add(membership);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task DeleteWithWorkProfileDataAsync(Membership membership, CancellationToken cancellationToken = default)
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
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountOrganizersAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        context.Memberships
            .CountAsync(m => m.OrganizationId == organizationId && m.Role == ERole.Organizer, cancellationToken);
}
