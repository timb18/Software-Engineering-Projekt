using DataAccess.Models;

namespace DataAccess.Repositories;

public interface IMembershipRepository
{
    Task<Membership?> FindAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<Membership?> FindWithWorkProfileAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<Membership?> FindOrganizerAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsMemberByEmailAsync(Guid organizationId, string normalizedEmail, CancellationToken cancellationToken = default);
    Task<Membership?> FindPersonalAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Membership membership, CancellationToken cancellationToken = default);
    Task DeleteWithWorkProfileDataAsync(Membership membership, CancellationToken cancellationToken = default);
}
