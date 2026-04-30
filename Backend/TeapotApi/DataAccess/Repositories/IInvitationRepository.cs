using DataAccess.Models;

namespace DataAccess.Repositories;

public interface IInvitationRepository
{
    Task<Invitation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Invitation?> FindOpenAsync(Guid organizationId, string normalizedEmail, CancellationToken cancellationToken = default);
    Task<IEnumerable<Invitation>> GetPendingForEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<IEnumerable<Invitation>> GetForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Invitation>> GetExpiredOpenAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default);
    Task UpdateAsync(Invitation invitation, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<Invitation> invitations, CancellationToken cancellationToken = default);
}
