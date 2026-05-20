using DataAccess.Models;
using DataAccess.Repositories;

namespace Services.WorkProfiles;

/// <summary>
/// Manages the personal work profile and its daily schedule layout.
/// </summary>
public class WorkProfileService(
    IWorkProfileRepository workProfileRepository,
    IMembershipRepository membershipRepository) : IWorkProfileService
{
    private static readonly string[] ValidDays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    /// <summary>
    /// Loads the personal work profile and fills any missing weekday entries.
    /// </summary>
    public async Task<WorkProfile?> GetAsync(Guid userId, Guid? organizationId = null, CancellationToken cancellationToken = default)
    {
        var profile = await workProfileRepository.GetPersonalNoTrackingAsync(userId, cancellationToken);
        if (profile is null) return null;

        var existingDays = profile.Days.ToDictionary(d => d.Day);
        profile.Days = ValidDays.Select(day =>
            existingDays.TryGetValue(day, out var existing)
                ? existing
                : new WorkDayProfile { Day = day, WorkProfileId = profile.Id }
        ).ToList();

        return profile;
    }

    /// <summary>
    /// Creates or updates the user's work profile and keeps nested day, block, and break graphs consistent.
    /// </summary>
    public async Task<WorkProfile> SaveAsync(Guid userId, WorkProfile profile, Guid? organizationId = null, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProfile(profile);
        ValidateProfile(normalized);

        var existing = await workProfileRepository.GetPersonalAsync(userId, cancellationToken);

        if (existing is null)
        {
            var membership = await membershipRepository.FindPersonalAsync(userId, cancellationToken);

            if (membership is null)
                throw new ArgumentException("No membership found for this user.");

            normalized.MembershipId = membership.Id;
            normalized.CreatedAt = DateTime.UtcNow;
            PrepareProfileGraph(normalized);
            await workProfileRepository.AddAsync(normalized, cancellationToken);
            return await GetAsync(userId, organizationId, cancellationToken) ?? normalized;
        }

        var oldDays = existing.Days.ToList();
        var scheduleChanged = HasSchedulingInputChanged(existing.MaxDailyLoad, oldDays, normalized.MaxDailyLoad, normalized.Days);

        existing.MaxDailyLoad = normalized.MaxDailyLoad;
        existing.PlannerViewStart = normalized.PlannerViewStart;
        existing.PlannerViewEnd = normalized.PlannerViewEnd;
        existing.EditedAt = DateTime.UtcNow;
        // Prepare foreign keys on new days without touching the tracked navigation property.
        // This avoids EF change-tracker conflicts from navigation-collection reassignment.
        var newDays = normalized.Days.Select(day =>
        {
            if (day.Id == Guid.Empty) day.Id = Guid.NewGuid();
            day.WorkProfileId = existing.Id;
            foreach (var block in day.Blocks)
            {
                if (block.Id == Guid.Empty) block.Id = Guid.NewGuid();
                block.WorkDayProfileId = day.Id;
            }
            foreach (var workBreak in day.Breaks)
            {
                if (workBreak.Id == Guid.Empty) workBreak.Id = Guid.NewGuid();
                workBreak.WorkDayProfileId = day.Id;
            }
            return day;
        }).ToList();

        // ReplaceDaysAsync calls SaveChangesAsync internally, which also persists
        // the scalar-property changes on the tracked entity.
        await workProfileRepository.ReplaceDaysAsync(existing.Id, oldDays, newDays, scheduleChanged, cancellationToken);

        return await GetAsync(userId, organizationId, cancellationToken) ?? existing;
    }

    /// <summary>
    /// Deletes the current user's work profile and its dependent data.
    /// </summary>
    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));

        var profile = await workProfileRepository.GetForDeleteByUserIdAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("Work profile not found.");

        await workProfileRepository.DeleteAsync(profile, cancellationToken);
    }

    /// <summary>
    /// Deletes the work profile identified by the given email address.
    /// </summary>
    public async Task DeleteByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var profile = await workProfileRepository.GetForDeleteByEmailAsync(normalizedEmail, cancellationToken)
            ?? throw new KeyNotFoundException("Work profile not found.");

        await workProfileRepository.DeleteAsync(profile, cancellationToken);
    }

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

    private static void ValidateProfile(WorkProfile profile)
    {
        foreach (var day in profile.Days)
        {
            var previousBlockEnd = TimeSpan.MinValue;
            var blockIndex = 0;
            foreach (var block in day.Blocks.OrderBy(b => ParseTime(b.StartTime)))
            {
                blockIndex++;
                var start = ParseTime(block.StartTime);
                var end = ParseTime(block.EndTime);

                if (string.IsNullOrWhiteSpace(block.CompanyName))
                    throw new ArgumentException($"{DayName(day.Day)} work block {blockIndex} needs a company.");
                if (end <= start)
                    throw new ArgumentException($"{DayName(day.Day)} work block {blockIndex} must end after it starts.");
                if (previousBlockEnd > start)
                    throw new ArgumentException($"{DayName(day.Day)} contains overlapping work blocks. Please keep each work block separate.");

                previousBlockEnd = end;
            }

            var previousBreakEnd = TimeSpan.MinValue;
            var breakIndex = 0;
            foreach (var workBreak in day.Breaks.OrderBy(b => ParseTime(b.StartTime)))
            {
                breakIndex++;
                var start = ParseTime(workBreak.StartTime);
                var end = ParseTime(workBreak.EndTime);

                if (end <= start)
                    throw new ArgumentException($"{DayName(day.Day)} break {breakIndex} must end after it starts.");
                if (previousBreakEnd > start)
                    throw new ArgumentException($"{DayName(day.Day)} contains overlapping breaks. Please keep each break separate.");

                previousBreakEnd = end;
            }
        }
    }

    private static TimeSpan ParseTime(string value)
    {
        if (!TimeSpan.TryParse(value, out var parsed))
            throw new ArgumentException($"Invalid time format: '{value}'. Expected HH:mm.");

        if (parsed < TimeSpan.Zero || parsed >= TimeSpan.FromDays(1))
            throw new ArgumentException($"Invalid time value: '{value}'. Expected a time between 00:00 and 23:59.");

        return parsed;
    }

    private static string DayName(string day) => day switch
    {
        "Mon" => "Monday",
        "Tue" => "Tuesday",
        "Wed" => "Wednesday",
        "Thu" => "Thursday",
        "Fri" => "Friday",
        "Sat" => "Saturday",
        "Sun" => "Sunday",
        _ => day
    };

    private static void PrepareProfileGraph(WorkProfile profile)
    {
        if (profile.Id == Guid.Empty)
            profile.Id = Guid.NewGuid();

        foreach (var day in profile.Days)
        {
            if (day.Id == Guid.Empty)
                day.Id = Guid.NewGuid();

            day.WorkProfileId = profile.Id;

            foreach (var block in day.Blocks)
            {
                if (block.Id == Guid.Empty)
                    block.Id = Guid.NewGuid();

                block.WorkDayProfileId = day.Id;
            }

            foreach (var workBreak in day.Breaks)
            {
                if (workBreak.Id == Guid.Empty)
                    workBreak.Id = Guid.NewGuid();

                workBreak.WorkDayProfileId = day.Id;
            }
        }
    }

    private static bool HasSchedulingInputChanged(
        TimeSpan oldMaxDailyLoad,
        IEnumerable<WorkDayProfile> oldDays,
        TimeSpan newMaxDailyLoad,
        IEnumerable<WorkDayProfile> newDays)
    {
        if (oldMaxDailyLoad != newMaxDailyLoad)
            return true;

        var oldByDay = oldDays.ToDictionary(day => day.Day);
        var newByDay = newDays.ToDictionary(day => day.Day);
        if (!oldByDay.Keys.Order().SequenceEqual(newByDay.Keys.Order()))
            return true;

        foreach (var day in newByDay.Keys)
        {
            var oldDay = oldByDay[day];
            var newDay = newByDay[day];

            var oldBlocks = oldDay.Blocks
                .OrderBy(block => block.StartTime)
                .ThenBy(block => block.EndTime)
                .ThenBy(block => block.CompanyId)
                .Select(block => (block.CompanyId, block.CompanyName, block.StartTime, block.EndTime));
            var newBlocks = newDay.Blocks
                .OrderBy(block => block.StartTime)
                .ThenBy(block => block.EndTime)
                .ThenBy(block => block.CompanyId)
                .Select(block => (block.CompanyId, block.CompanyName, block.StartTime, block.EndTime));
            if (!oldBlocks.SequenceEqual(newBlocks))
                return true;

            var oldBreaks = oldDay.Breaks
                .OrderBy(workBreak => workBreak.StartTime)
                .ThenBy(workBreak => workBreak.EndTime)
                .Select(workBreak => (workBreak.StartTime, workBreak.EndTime));
            var newBreaks = newDay.Breaks
                .OrderBy(workBreak => workBreak.StartTime)
                .ThenBy(workBreak => workBreak.EndTime)
                .Select(workBreak => (workBreak.StartTime, workBreak.EndTime));
            if (!oldBreaks.SequenceEqual(newBreaks))
                return true;
        }

        return false;
    }
}
