using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Services;

public class WorkProfileService(
    TeapotDbContext dbContext,
    IGenericRepository<WorkProfile> repository,
    IGenericRepository<Membership> membershipRepository) : IWorkProfileService
{
    private static readonly string[] ValidDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    public async Task<WorkProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await repository.GetQueryable()
            .Include(wp => wp.Days)
                .ThenInclude(d => d.Blocks)
            .Include(wp => wp.Days)
                .ThenInclude(d => d.Breaks)
            .FirstOrDefaultAsync(wp => wp.Membership.UserId == userId, cancellationToken);

        if (profile is null) return null;

        var existingDays = profile.Days.ToDictionary(d => d.Day);
        profile.Days = ValidDays.Select(day =>
            existingDays.TryGetValue(day, out var existing)
                ? existing
                : new WorkDayProfile { Day = day, WorkProfileId = profile.Id }
        ).ToList();

        return profile;
    }

    public async Task<WorkProfile> SaveAsync(Guid userId, WorkProfile profile, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetQueryable()
            .Include(wp => wp.Days).ThenInclude(d => d.Blocks)
            .Include(wp => wp.Days).ThenInclude(d => d.Breaks)
            .FirstOrDefaultAsync(wp => wp.Membership.UserId == userId, cancellationToken);

        var normalized = NormalizeProfile(profile);

        if (existing is null)
        {
            var membership = await membershipRepository.GetQueryable()
                .FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken)
                ?? throw new ArgumentException("No membership found for this user.");

            normalized.MembershipId = membership.Id;
            normalized.CreatedAt = DateTime.UtcNow;
            await repository.AddAsync(normalized, cancellationToken);
        }
        else
        {
            existing.MaxDailyLoad = normalized.MaxDailyLoad;
            existing.PlannerViewStart = normalized.PlannerViewStart;
            existing.PlannerViewEnd = normalized.PlannerViewEnd;
            existing.EditedAt = DateTime.UtcNow;
            existing.Days.Clear();
            foreach (var day in normalized.Days)
            {
                day.WorkProfileId = existing.Id;
                existing.Days.Add(day);
            }
            await repository.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        return normalized;
    }

    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        var existing = await repository.GetQueryable()
            .Include(wp => wp.Days).ThenInclude(d => d.Blocks)
            .Include(wp => wp.Days).ThenInclude(d => d.Breaks)
            .FirstOrDefaultAsync(wp => wp.Membership.UserId == userId, cancellationToken);

        if (existing is null)
        {
            throw new KeyNotFoundException("Work profile not found.");
        }

        var workProfileId = existing.Id;

        var userTasks = await dbContext.UserTasks
            .Where(task => task.WorkProfileId == workProfileId)
            .ToListAsync(cancellationToken);

        if (userTasks.Count > 0)
        {
            dbContext.UserTasks.RemoveRange(userTasks);
        }

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

        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                @"DELETE FROM work_profile_time_intervals WHERE work_profile_id = @workProfileId",
                [new NpgsqlParameter("workProfileId", workProfileId)],
                cancellationToken);
        }

        dbContext.WorkProfiles.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Ensures the profile has exactly one entry per weekday in Mon–Sun order,
    /// and that blocks/breaks within each day are sorted by start time.
    /// </summary>
    private static WorkProfile NormalizeProfile(WorkProfile profile)
    {
        var lookup = profile.Days
            .GroupBy(d => d.Day)
            .ToDictionary(g => g.Key, g => g.First());

        var normalizedDays = ValidDays.Select(day =>
        {
            if (!lookup.TryGetValue(day, out var existing))
                return new WorkDayProfile { Day = day };

            existing.Blocks = [.. existing.Blocks.OrderBy(b => b.StartTime)];
            existing.Breaks = [.. existing.Breaks.OrderBy(b => b.StartTime)];
            return existing;
        }).ToList();

        profile.Days = normalizedDays;
        return profile;
    }
}
