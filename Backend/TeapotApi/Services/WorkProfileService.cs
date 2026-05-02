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
    private const string PersonalWorkspaceDescription = "Personal workspace";

    public async Task<WorkProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await repository.GetQueryable()
            .Include(wp => wp.WorkDayProfiles)
                .ThenInclude(d => d.WorkBlocks)
            .Include(wp => wp.WorkDayProfiles)
                .ThenInclude(d => d.WorkBreaks)
            .Include(wp => wp.Membership)
                .ThenInclude(m => m.Organization)
            .Where(wp => wp.Membership.UserId == userId)
            .OrderByDescending(wp =>
                wp.Membership.Role == ERole.Organizer &&
                wp.Membership.Organization.MaxUsers == 1 &&
                wp.Membership.Organization.Description == PersonalWorkspaceDescription)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null) return null;

        var existingDays = profile.WorkDayProfiles.ToDictionary(d => d.Day);
        profile.WorkDayProfiles = ValidDays.Select(day =>
            existingDays.TryGetValue(day, out var existing)
                ? existing
                : new WorkDayProfile { Day = day, WorkProfileId = profile.Id }
        ).ToList();

        return profile;
    }

    public async Task<WorkProfile> SaveAsync(Guid userId, WorkProfile profile, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetQueryable()
            .Include(wp => wp.WorkDayProfiles).ThenInclude(d => d.WorkBlocks)
            .Include(wp => wp.WorkDayProfiles).ThenInclude(d => d.WorkBreaks)
            .Include(wp => wp.Membership)
                .ThenInclude(m => m.Organization)
            .Where(wp => wp.Membership.UserId == userId)
            .OrderByDescending(wp =>
                wp.Membership.Role == ERole.Organizer &&
                wp.Membership.Organization.MaxUsers == 1 &&
                wp.Membership.Organization.Description == PersonalWorkspaceDescription)
            .FirstOrDefaultAsync(cancellationToken);

        var normalized = NormalizeProfile(profile);

        if (existing is null)
        {
            var membership = await membershipRepository.GetQueryable()
                .Include(m => m.Organization)
                .Where(m => m.UserId == userId)
                .OrderByDescending(m =>
                    m.Role == ERole.Organizer &&
                    m.Organization.MaxUsers == 1 &&
                    m.Organization.Description == PersonalWorkspaceDescription)
                .FirstOrDefaultAsync(cancellationToken)
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
            existing.WorkDayProfiles.Clear();
            foreach (var day in normalized.WorkDayProfiles)
            {
                day.WorkProfileId = existing.Id;
                existing.WorkDayProfiles.Add(day);
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
            .Include(wp => wp.WorkDayProfiles).ThenInclude(d => d.WorkBlocks)
            .Include(wp => wp.WorkDayProfiles).ThenInclude(d => d.WorkBreaks)
            .FirstOrDefaultAsync(wp => wp.Membership.UserId == userId, cancellationToken);

        await DeleteExistingProfileAsync(existing, cancellationToken);
    }

    public async Task DeleteByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var existing = await repository.GetQueryable()
            .Include(wp => wp.WorkDayProfiles).ThenInclude(d => d.WorkBlocks)
            .Include(wp => wp.WorkDayProfiles).ThenInclude(d => d.WorkBreaks)
            .FirstOrDefaultAsync(wp => wp.Membership.User.Email.ToLower() == normalizedEmail, cancellationToken);

        await DeleteExistingProfileAsync(existing, cancellationToken);
    }

    private async Task DeleteExistingProfileAsync(WorkProfile? existing, CancellationToken cancellationToken)
    {
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
        var lookup = profile.WorkDayProfiles
            .GroupBy(d => d.Day)
            .ToDictionary(g => g.Key, g => g.First());

        var normalizedDays = ValidDays.Select(day =>
        {
            if (!lookup.TryGetValue(day, out var existing))
                return new WorkDayProfile { Day = day };

            existing.WorkBlocks = [.. existing.WorkBlocks.OrderBy(b => b.StartTime)];
            existing.WorkBreaks = [.. existing.WorkBreaks.OrderBy(b => b.StartTime)];
            return existing;
        }).ToList();

        profile.WorkDayProfiles = normalizedDays;
        return profile;
    }
}
