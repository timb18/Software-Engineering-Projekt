using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class WorkProfileRepository(TeapotDbContext context) : IWorkProfileRepository
{
    public Task<WorkProfile?> GetPersonalAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return context.WorkProfiles
            .Include(wp => wp.Days).ThenInclude(d => d.Blocks)
            .Include(wp => wp.Days).ThenInclude(d => d.Breaks)
            .Include(wp => wp.Membership).ThenInclude(m => m.Organization)
            .Where(wp => wp.Membership.UserId == userId)
            .OrderByDescending(wp =>
                wp.Membership.Role == ERole.Organizer &&
                wp.Membership.Organization.MaxUsers == 1)
            .ThenBy(wp => wp.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<WorkProfile?> GetPersonalNoTrackingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return context.WorkProfiles
            .AsNoTracking()
            .Include(wp => wp.Days).ThenInclude(d => d.Blocks)
            .Include(wp => wp.Days).ThenInclude(d => d.Breaks)
            .Include(wp => wp.Membership).ThenInclude(m => m.Organization)
            .Where(wp => wp.Membership.UserId == userId)
            .OrderByDescending(wp =>
                wp.Membership.Role == ERole.Organizer &&
                wp.Membership.Organization.MaxUsers == 1)
            .ThenBy(wp => wp.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<WorkProfile?> GetByUserAndOrganizationAsync(Guid userId, Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return GetPersonalAsync(userId, cancellationToken);
    }

    public Task<WorkProfile?> GetByUserAndOrganizationNoTrackingAsync(Guid userId, Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return GetPersonalNoTrackingAsync(userId, cancellationToken);
    }

    public Task<WorkProfile?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return context.WorkProfiles
            .Where(wp => wp.Membership.UserId == userId)
            .OrderByDescending(wp =>
                wp.Membership.Role == ERole.Organizer &&
                wp.Membership.Organization.MaxUsers == 1)
            .ThenBy(wp => wp.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<WorkProfile?> GetByIdAsync(Guid workProfileId, CancellationToken cancellationToken = default)
    {
        return context.WorkProfiles
            .Include(wp => wp.Days).ThenInclude(d => d.Blocks)
            .Include(wp => wp.Days).ThenInclude(d => d.Breaks)
            .Include(wp => wp.Membership).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(wp => wp.Id == workProfileId, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkProfileTimeInterval>> GetTimeIntervalsAsync(
        Guid workProfileId, CancellationToken cancellationToken = default)
    {
        return await context.WorkProfileTimeIntervals
            .Where(i => i.WorkProfileId == workProfileId)
            .ToListAsync(cancellationToken);
    }

    public Task<WorkProfile?> GetForDeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return context.WorkProfiles
            .Include(wp => wp.Days).ThenInclude(d => d.Blocks)
            .Include(wp => wp.Days).ThenInclude(d => d.Breaks)
            .Where(wp => wp.Membership.UserId == userId)
            .OrderByDescending(wp =>
                wp.Membership.Role == ERole.Organizer &&
                wp.Membership.Organization.MaxUsers == 1)
            .ThenBy(wp => wp.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<WorkProfile?> GetForDeleteByEmailAsync(string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        return context.WorkProfiles
            .Include(wp => wp.Days).ThenInclude(d => d.Blocks)
            .Include(wp => wp.Days).ThenInclude(d => d.Breaks)
            .Where(wp => wp.Membership.User.Email == normalizedEmail)
            .OrderByDescending(wp =>
                wp.Membership.Role == ERole.Organizer &&
                wp.Membership.Organization.MaxUsers == 1)
            .ThenBy(wp => wp.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(WorkProfile profile, CancellationToken cancellationToken = default)
    {
        context.WorkProfiles.Add(profile);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceDaysAsync(
        Guid workProfileId,
        IList<WorkDayProfile> oldDays,
        IList<WorkDayProfile> newDays,
        bool deleteFlexibleTaskBlocks = false,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);

        if (deleteFlexibleTaskBlocks)
        {
            var taskIds = await context.UserTasks
                .Where(t => t.WorkProfileId == workProfileId)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            var flexibleTaskBlocks = await context.TaskBlocks
                .Where(block => taskIds.Contains(block.TaskId) && !block.IsFixed)
                .ToListAsync(cancellationToken);

            if (flexibleTaskBlocks.Count > 0)
                context.TaskBlocks.RemoveRange(flexibleTaskBlocks);
        }

        if (oldDays.Count > 0)
        {
            var dayIds = oldDays.Select(d => d.Id).ToList();

            var oldBlocks = await context.WorkBlocks
                .Where(b => dayIds.Contains(b.WorkDayProfileId))
                .ToListAsync(cancellationToken);
            var oldBreaks = await context.WorkBreaks
                .Where(b => dayIds.Contains(b.WorkDayProfileId))
                .ToListAsync(cancellationToken);

            if (oldBlocks.Count > 0) context.WorkBlocks.RemoveRange(oldBlocks);
            if (oldBreaks.Count > 0) context.WorkBreaks.RemoveRange(oldBreaks);
            context.WorkDayProfiles.RemoveRange(oldDays);
            await context.SaveChangesAsync(cancellationToken);
        }

        await context.WorkDayProfiles.AddRangeAsync(newDays, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(WorkProfile profile, CancellationToken cancellationToken = default)
    {
        await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);

        var workProfileId = profile.Id;

        var userTasks = await context.UserTasks
            .Where(t => t.WorkProfileId == workProfileId)
            .ToListAsync(cancellationToken);
        if (userTasks.Count > 0) context.UserTasks.RemoveRange(userTasks);

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

        context.WorkProfiles.Remove(profile);
        await context.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
    }
}