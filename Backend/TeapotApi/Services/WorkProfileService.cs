using DataAccess.Models;
using DataAccess.Repositories;

namespace Services;

public class WorkProfileService(IGenericRepository<WorkProfile> repository) : IWorkProfileService
{
    private static readonly string[] ValidDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    public Task<WorkProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return repository.GetFirstOrDefaultAsync(wp => wp.Membership.UserId == userId, cancellationToken);
    }

    public async Task<WorkProfile> SaveAsync(WorkProfile profile, CancellationToken cancellationToken = default)
    {
        // var normalized = NormalizeProfile(profile);
        await repository.UpdateAsync(profile, cancellationToken);
        return profile;
    }

    /*/// <summary>
    /// Ensures the profile has exactly one entry per weekday in Mon–Sun order,
    /// and that blocks/breaks within each day are sorted by start time.
    /// </summary>
    private static WorkProfile NormalizeProfile(WorkProfile profile)
    {
        var lookup = profile.Days.ToDictionary(d => d.Day);

        var normalizedDays = ValidDays.Select(day =>
        {
            if (!lookup.TryGetValue(day, out var existing))
                return new WorkDayProfile { Day = day };

            return existing with
            {
                Blocks = [.. existing.Blocks.OrderBy(b => b.StartTime)],
                Breaks = [.. existing.Breaks.OrderBy(b => b.StartTime)],
            };
        }).ToList();

        return profile with { Days = normalizedDays };
    }*/
}