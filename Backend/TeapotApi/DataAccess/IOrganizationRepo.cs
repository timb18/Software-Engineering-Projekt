using Model;

namespace DataAccess;

public interface IOrganizationRepo
{
    Task<Organization?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<Organization> CreateAsync(Organization organization, CancellationToken cancellationToken = default);
}
