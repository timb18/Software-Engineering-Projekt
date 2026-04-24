using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class MembershipService(TeapotDbContext dbContext) : IMembershipService
{
    public async Task LeaveOrganizationAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        var membership = await dbContext.Memberships
            .Include(m => m.WorkProfile)
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.OrganizationId == organizationId,
                cancellationToken);

        if (membership is null)
        {
            throw new KeyNotFoundException("Membership not found.");
        }

        if (membership.WorkProfile is not null)
        {
            var workProfileId = membership.WorkProfile.Id;

            var workDayProfiles = await dbContext.WorkDayProfiles
                .Where(day => day.WorkProfileId == workProfileId)
                .ToListAsync(cancellationToken);

            if (workDayProfiles.Count > 0)
            {
                var workDayProfileIds = workDayProfiles.Select(day => day.Id).ToList();

                var workBlocks = await dbContext.WorkBlocks
                    .Where(block => workDayProfileIds.Contains(block.WorkDayProfileId))
                    .ToListAsync(cancellationToken);

                var workBreaks = await dbContext.WorkBreaks
                    .Where(workBreak => workDayProfileIds.Contains(workBreak.WorkDayProfileId))
                    .ToListAsync(cancellationToken);

                if (workBlocks.Count > 0)
                {
                    dbContext.WorkBlocks.RemoveRange(workBlocks);
                }

                if (workBreaks.Count > 0)
                {
                    dbContext.WorkBreaks.RemoveRange(workBreaks);
                }

                dbContext.WorkDayProfiles.RemoveRange(workDayProfiles);
            }

            var timeIntervals = await dbContext.WorkProfileTimeIntervals
                .Where(interval => interval.WorkProfileId == workProfileId)
                .ToListAsync(cancellationToken);

            if (timeIntervals.Count > 0)
            {
                dbContext.WorkProfileTimeIntervals.RemoveRange(timeIntervals);
            }

            var userTasks = await dbContext.UserTasks
                .Where(task => task.WorkProfileId == workProfileId)
                .ToListAsync(cancellationToken);

            if (userTasks.Count > 0)
            {
                dbContext.UserTasks.RemoveRange(userTasks);
            }

            dbContext.WorkProfiles.Remove(membership.WorkProfile);
        }

        dbContext.Memberships.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
