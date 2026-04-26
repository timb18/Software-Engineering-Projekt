using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class WorkProfileService(
    IGenericRepository<WorkProfile> repository,
    IGenericRepository<Membership> membershipRepository) : IWorkProfileService
{
    private static readonly string[] ValidDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
    private const string PersonalWorkspaceDescription = "Personal workspace";

    public async Task<WorkProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await repository.GetQueryable()
            .Include(wp => wp.Days)
                .ThenInclude(d => d.Blocks)
            .Include(wp => wp.Days)
                .ThenInclude(d => d.Breaks)
            .Include(wp => wp.Membership)
                .ThenInclude(m => m.Organization)
            .Where(wp => wp.Membership.UserId == userId)
            .OrderByDescending(wp =>
                wp.Membership.Role == ERole.Organizer &&
                wp.Membership.Organization.MaxUsers == 1 &&
                wp.Membership.Organization.Description == PersonalWorkspaceDescription)
            .FirstOrDefaultAsync(cancellationToken);

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
