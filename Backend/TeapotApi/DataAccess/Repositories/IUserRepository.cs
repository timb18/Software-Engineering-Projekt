using DataAccess.Models;

namespace DataAccess.Repositories;

public interface IUserRepository
{
    Task<User?> FindByAuthProviderSubjectAsync(string subject, CancellationToken cancellationToken = default);
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> IsEmailTakenByOtherAsync(Guid userId, string normalizedEmail, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}
