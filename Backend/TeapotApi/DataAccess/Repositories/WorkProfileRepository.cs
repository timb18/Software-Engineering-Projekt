using DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DataAccess.Repositories;

public class WorkProfileRepository(TeapotDbContext context) : IWorkProfileRepository
{
    private const string PersonalWorkspaceDescription = "Personal workspace";

    public Task<WorkProfile?> GetPersonalAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.WorkProfiles
            .Include(wp => wp.Days).ThenInclude(d => d.Blocks)
            .Include(wp => wp.Days).ThenInclude(d => d.Breaks)
            .Include(wp => wp.Membership).ThenInclude(m => m.Organization)
            .Where(wp => wp.Membership.UserId == userId)
            .OrderByDescending(wp =>
                wp.Membership.Role == ERole.Organizer &&
                wp.Membership.Organization.MaxUsers == 1 &&
                wp.Membership.Organization.Description == PersonalWorkspaceDescription)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<WorkProfile?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.WorkProfiles
            .FirstOrDefaultAsync(wp => wp.Membership.UserId == userId, cancellationToken);

    public Task<WorkProfile?> GetForDeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.WorkProfiles
            .Include(wp => wp.Days).ThenInclude(d => d.Blocks)
            .Include(wp => wp.Days).ThenInclude(d => d.Breaks)
            .FirstOrDefaultAsync(wp => wp.Membership.UserId == userId, cancellationToken);

    public Task<WorkProfile?> GetForDeleteByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        context.WorkProfiles
            .Include(wp => wp.Days).ThenInclude(d => d.Blocks)
            .Include(wp => wp.Days).ThenInclude(d => d.Breaks)
            .FirstOrDefaultAsync(wp => wp.Membership.User.Email.ToLower() == normalizedEmail, cancellationToken);

    public async Task AddAsync(WorkProfile profile, CancellationToken cancellationToken = default)
    {
        context.WorkProfiles.Add(profile);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceDaysAsync(IList<WorkDayProfile> oldDays, IList<WorkDayProfile> newDays, CancellationToken cancellationToken = default)
    {
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
    }

    public async Task DeleteAsync(WorkProfile profile, CancellationToken cancellationToken = default)
    {
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

        if (context.Database.IsRelational())
        {
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM work_profile_time_intervals WHERE work_profile_id = @workProfileId",
                [new NpgsqlParameter("workProfileId", workProfileId)],
                cancellationToken);
        }

        context.WorkProfiles.Remove(profile);
        await context.SaveChangesAsync(cancellationToken);
    }
}
