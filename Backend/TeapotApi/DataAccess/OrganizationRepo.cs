using Model;

namespace DataAccess;

public class OrganizationRepo : IOrganizationRepo
{
    private readonly List<Organization> _organizations = [];
    private readonly object _sync = new();

    public Task<Organization?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            Organization? organization = _organizations.FirstOrDefault(x =>
                string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(organization);
        }
    }

    public Task<Organization> CreateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _organizations.Add(organization);
        }

        return Task.FromResult(organization);
    }
}
