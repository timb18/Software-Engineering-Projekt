namespace Services;

public interface IUserService
{
    Task<(Guid UserId, Guid WorkProfileId)> EnsureUserAsync(string email, CancellationToken cancellationToken = default);
}
