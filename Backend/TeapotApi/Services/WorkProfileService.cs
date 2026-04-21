using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class WorkProfileService(
    IGenericRepository<WorkProfile> repository,
    IGenericRepository<Membership> membershipRepository) : IWorkProfileService
{
    private static readonly string[] ValidDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    public Task<WorkProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return repository.GetQueryable()
            .Include(wp => wp.Days)
                .ThenInclude(d => d.Blocks)
            .Include(wp => wp.Days)
                .ThenInclude(d => d.Breaks)
            .FirstOrDefaultAsync(wp => wp.Membership.UserId == userId, cancellationToken);
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
            await repository.AddAsync(normalized, cancellationToken);
        }
        else
        {
            normalized.Id = existing.Id;
            normalized.MembershipId = existing.MembershipId;
            normalized.CreatedAt = existing.CreatedAt;
            normalized.EditedAt = DateTime.UtcNow;
            await repository.UpdateAsync(normalized, cancellationToken);
        }

        return normalized;
    }

    /// <summary>
    /// Ensures the profile has exactly one entry per weekday in Mon–Sun order,
    /// and that blocks/breaks within each day are sorted by start time.
    /// </summary>
    private static WorkProfile NormalizeProfile(WorkProfile profile)
    {
        var lookup = profile.Days.ToDictionary(d => d.Day);

        var normalizedDays = ValidDays.Select(day =>
        {
            if (!lookup.TryGetValue(day, out var existing))
                return new WorkDayProfile { WorkProfileId = profile.Id, Day = day };

            existing.Blocks = [.. existing.Blocks.OrderBy(b => b.StartTime)];
            existing.Breaks = [.. existing.Breaks.OrderBy(b => b.StartTime)];
            return existing;
        }).ToList();

        profile.Days = normalizedDays;
        return profile;
    }
}