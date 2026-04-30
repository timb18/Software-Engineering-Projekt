using DataAccess.Models;

namespace DataAccess.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Organization?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<Organization>> GetForUserAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task<Organization?> GetWithMembershipsAndInvitationsAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Organization organization, CancellationToken cancellationToken = default);
    Task DeleteWithCascadeAsync(Organization organization, CancellationToken cancellationToken = default);
}
