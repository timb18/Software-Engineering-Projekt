using Model;

namespace DataAccess;

public class FakeOrganizationRepo : IOrganizationRepo
{
    private readonly List<Organization> _fakeOrganizationsDb = [];
    private readonly object _sync = new();

    public Task<Organization?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            Organization? organization = _fakeOrganizationsDb.FirstOrDefault(x =>
                string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(organization);
        }
    }

    public Task<Organization> CreateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _fakeOrganizationsDb.Add(organization);
        }

        return Task.FromResult(organization);
    }
}
