namespace Services;

public interface IUserService
{
    /// <summary>
    /// Finds or creates a user by email. Also ensures the user has a personal
    /// work profile so tasks can be persisted. Returns the user's DB id and
    /// personal work-profile id.
    /// </summary>
    Task<(Guid UserId, Guid WorkProfileId)> EnsureUserAsync(string email, CancellationToken cancellationToken = default);
}