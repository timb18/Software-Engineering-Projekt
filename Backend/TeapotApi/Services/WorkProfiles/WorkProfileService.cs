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
        var profile = organizationId.HasValue
            ? await workProfileRepository.GetByUserAndOrganizationNoTrackingAsync(userId, organizationId.Value, cancellationToken)
            : await workProfileRepository.GetPersonalNoTrackingAsync(userId, cancellationToken);
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
        var existing = organizationId.HasValue
            ? await workProfileRepository.GetByUserAndOrganizationAsync(userId, organizationId.Value, cancellationToken)
            : await workProfileRepository.GetPersonalAsync(userId, cancellationToken);
        var normalized = NormalizeProfile(profile);

        if (existing is null)
        {
            var membership = organizationId.HasValue
                ? await membershipRepository.FindAsync(userId, organizationId.Value, cancellationToken)
                : await membershipRepository.FindPersonalAsync(userId, cancellationToken);

            if (membership is null)
                throw new ArgumentException("No membership found for this user and organization.");

            normalized.MembershipId = membership.Id;
            normalized.CreatedAt = DateTime.UtcNow;
            PrepareProfileGraph(normalized);
            await workProfileRepository.AddAsync(normalized, cancellationToken);
            return await GetAsync(userId, organizationId, cancellationToken) ?? normalized;
        }

        existing.MaxDailyLoad = normalized.MaxDailyLoad;
        existing.PlannerViewStart = normalized.PlannerViewStart;
        existing.PlannerViewEnd = normalized.PlannerViewEnd;
        existing.EditedAt = DateTime.UtcNow;

        var oldDays = existing.Days.ToList();
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
        await workProfileRepository.ReplaceDaysAsync(oldDays, newDays, cancellationToken);

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
}
