using DataAccess;
using Model;

namespace Services;

public class WorkProfileService(IWorkProfileRepository repository) : IWorkProfileService
{
    private static readonly string[] ValidDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    public Task<WorkProfile?> GetAsync(string userId) =>
        repository.GetByUserIdAsync(userId);

    public Task<WorkProfile> SaveAsync(WorkProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.UserId))
            throw new ArgumentException("UserId must not be empty.", nameof(profile));

        var normalized = NormalizeProfile(profile);
        return repository.UpsertAsync(normalized);
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
                return new WorkDayProfile { Day = day };

            return existing with
            {
                Blocks = [.. existing.Blocks.OrderBy(b => b.StartTime)],
                Breaks = [.. existing.Breaks.OrderBy(b => b.StartTime)],
            };
        }).ToList();

        return profile with { Days = normalizedDays };
    }
}
